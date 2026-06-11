using System;
using System.Collections.Generic;
using Timberborn.Automation;
using Timberborn.AutomationBuildings;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Illumination;
using Timberborn.Persistence;
using Timberborn.RelationSystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace Calloatti.AutoTweaks
{
  public class RelayColorReplicator : BaseComponent, IAwakableComponent, IPersistentEntity, IFinishedStateListener
  {
    private static readonly ComponentKey ReplicatorKey = new ComponentKey("RelayColorReplicator");
    private static readonly PropertyKey<bool> IsEnabledKey = new PropertyKey<bool>("IsColorReplicationEnabled");

    private Relay _relay;
    private Automator _automator;
    private CustomizableIlluminator _customizableIlluminator;

    private readonly List<CustomizableIlluminator> _subscribedIlluminators = new List<CustomizableIlluminator>();

    // FIX: Swapped out the brittle sequential list for a precise dictionary keyed by transmitter to prevent UI index desyncs
    private readonly Dictionary<Automator, bool> _prevActiveStatesMap = new Dictionary<Automator, bool>();
    private int _triggeringInputIndex = 0;

    public bool IsColorReplicationEnabled { get; private set; }

    public void Awake()
    {
      _relay = GetComponent<Relay>();
      _automator = GetComponent<Automator>();
      _customizableIlluminator = GetComponent<CustomizableIlluminator>();
    }

    public void Save(IEntitySaver entitySaver)
    {
      IObjectSaver component = entitySaver.GetComponent(ReplicatorKey);
      component.Set(IsEnabledKey, IsColorReplicationEnabled);
    }

    public void Load(IEntityLoader entityLoader)
    {
      if (entityLoader.TryGetComponent(ReplicatorKey, out var objectLoader))
      {
        IsColorReplicationEnabled = objectLoader.Has(IsEnabledKey) && objectLoader.Get(IsEnabledKey);
      }
    }

    public void OnEnterFinishedState()
    {
      ResubscribeToInputColors();
      ReplicateInputColors();
      ((IRelationOwner)_automator).RelationsChanged += OnRelationsChanged;
    }

    public void OnExitFinishedState()
    {
      ((IRelationOwner)_automator).RelationsChanged -= OnRelationsChanged;
      UnsubscribeFromInputColors();
    }

    public void SetColorReplicationEnabled(bool value)
    {
      if (IsColorReplicationEnabled != value)
      {
        IsColorReplicationEnabled = value;
        ResubscribeToInputColors();
        ReplicateInputColors();
      }
    }

    public void EvaluateColors()
    {
      if (IsColorReplicationEnabled)
      {
        ReplicateInputColors();
      }
    }

    private void OnRelationsChanged(object sender, EventArgs e)
    {
      ResubscribeToInputColors();
      ReplicateInputColors();
    }

    private void ResubscribeToInputColors()
    {
      UnsubscribeFromInputColors();
      if (!IsColorReplicationEnabled) return;

      for (int i = 0; i < _relay.Inputs.Count; i++)
      {
        var inputConn = _relay.Inputs[i];
        if (inputConn.Transmitter != null)
        {
          var illum = inputConn.Transmitter.GetComponent<CustomizableIlluminator>();
          if (illum != null && illum)
          {
            illum.CustomColorChanged += OnInputCustomColorChanged;
            _subscribedIlluminators.Add(illum);
          }
        }
      }

      if (_subscribedIlluminators.Count > 0)
      {
        _customizableIlluminator.Lock();
      }
    }

    private void UnsubscribeFromInputColors()
    {
      foreach (var illum in _subscribedIlluminators)
      {
        if (illum != null && illum)
        {
          illum.CustomColorChanged -= OnInputCustomColorChanged;
        }
      }
      _subscribedIlluminators.Clear();
      _customizableIlluminator.Unlock();
    }

    private void OnInputCustomColorChanged(object sender, EventArgs e)
    {
      ReplicateInputColors();
    }

    private void ReplicateInputColors()
    {
      if (!IsColorReplicationEnabled) return;

      int inputCount = _relay.Inputs.Count;
      int newTriggerIndex = _triggeringInputIndex;

      // 1. AND MODE: Triggers when the absolute LAST required input flips to True
      if (_relay.Mode == RelayMode.And)
      {
        bool allActive = true;
        int lastToTurnOn = _triggeringInputIndex;
        for (int i = 0; i < inputCount; i++)
        {
          bool currentActive = _relay.Inputs[i].IsConnected && _relay.Inputs[i].BooleanState;
          if (!currentActive) allActive = false;

          var tx = _relay.Inputs[i].Transmitter;
          if (tx != null)
          {
            _prevActiveStatesMap.TryGetValue(tx, out bool wasActive);
            if (currentActive && !wasActive) lastToTurnOn = i;
          }
        }
        if (allActive) newTriggerIndex = lastToTurnOn;
      }
      // 2. OR MODE: The most recent single input to turn ON gains color dominance
      else if (_relay.Mode == RelayMode.Or)
      {
        for (int i = 0; i < inputCount; i++)
        {
          bool currentActive = _relay.Inputs[i].IsConnected && _relay.Inputs[i].BooleanState;
          var tx = _relay.Inputs[i].Transmitter;
          if (tx != null)
          {
            _prevActiveStatesMap.TryGetValue(tx, out bool wasActive);
            if (currentActive && !wasActive) newTriggerIndex = i;
          }
        }

        if (newTriggerIndex >= inputCount || !_relay.Inputs[newTriggerIndex].BooleanState)
        {
          for (int i = 0; i < inputCount; i++)
          {
            if (_relay.Inputs[i].IsConnected && _relay.Inputs[i].BooleanState)
            {
              newTriggerIndex = i;
              break;
            }
          }
        }
      }
      // 3. XOR MODE: Pull color from the first active node processing the loop
      else if (_relay.Mode == RelayMode.Xor)
      {
        for (int i = 0; i < inputCount; i++)
        {
          if (_relay.Inputs[i].IsConnected && _relay.Inputs[i].BooleanState)
          {
            newTriggerIndex = i;
            break;
          }
        }
      }
      else
      {
        newTriggerIndex = 0;
      }

      // FIX: Guard against out-of-bounds indices if a wire/row was just deleted in the UI
      if (newTriggerIndex >= inputCount)
      {
        newTriggerIndex = 0;
      }

      // Commit states locked securely to the transmitter pointers
      _prevActiveStatesMap.Clear();
      for (int i = 0; i < inputCount; i++)
      {
        var tx = _relay.Inputs[i].Transmitter;
        if (tx != null)
        {
          _prevActiveStatesMap[tx] = _relay.Inputs[i].IsConnected && _relay.Inputs[i].BooleanState;
        }
      }
      _triggeringInputIndex = newTriggerIndex;

      if (_automator.UnfinishedState != AutomatorState.On)
      {
        return;
      }

      Color? finalColor = null;
      if (_triggeringInputIndex < inputCount)
      {
        var triggeringTransmitter = _relay.Inputs[_triggeringInputIndex].Transmitter;
        if (triggeringTransmitter != null)
        {
          var illum = triggeringTransmitter.GetComponent<CustomizableIlluminator>();
          if (illum != null) finalColor = illum.CustomColor;
        }
      }

      if (finalColor.HasValue)
      {
        _customizableIlluminator.SetIsCustomized(true);
        _customizableIlluminator.SetCustomColor(finalColor);
      }
    }
  }
}