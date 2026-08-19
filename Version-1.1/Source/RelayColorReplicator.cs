using System;
using System.Collections.Generic;
using System.Linq;
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

    private readonly List<Automator> _activationHistory = new List<Automator>(8);

    private readonly Dictionary<Automator, bool> _prevStateMap = new Dictionary<Automator, bool>();

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
      SeedActivationHistory();
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
        if (!value)
        {
          UnsubscribeFromInputColors();
        }
        else
        {
          ResubscribeToInputColors();
        }
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

      var currentTransmitters = new HashSet<Automator>();
      for (int i = 0; i < _relay.Inputs.Count; i++)
      {
        var inputConn = _relay.Inputs[i];
        if (inputConn.Transmitter != null)
        {
          currentTransmitters.Add(inputConn.Transmitter);
          var illum = inputConn.Transmitter.GetComponent<CustomizableIlluminator>();
          if (illum != null && illum)
          {
            illum.CustomColorChanged += OnInputCustomColorChanged;
            _subscribedIlluminators.Add(illum);
          }
        }
      }

      // Clean up _prevStateMap for transmitters no longer connected
      foreach (var tx in _prevStateMap.Keys.ToList())
      {
        if (!currentTransmitters.Contains(tx))
        {
          _prevStateMap.Remove(tx);
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

    private void SeedActivationHistory()
    {
      _activationHistory.Clear();
      int inputCount = _relay.Inputs.Count;
      for (int i = inputCount - 1; i >= 0; i--)
      {
        var tx = _relay.Inputs[i].Transmitter;
        if (tx != null && _relay.Inputs[i].IsConnected && _relay.Inputs[i].BooleanState)
        {
          _activationHistory.Add(tx);
        }
      }
    }

    private void ReplicateInputColors()
    {
      if (!IsColorReplicationEnabled) return;

      int inputCount = _relay.Inputs.Count;

      for (int i = 0; i < inputCount; i++)
      {
        var tx = _relay.Inputs[i].Transmitter;
        if (tx == null) continue;

        bool isActive = _relay.Inputs[i].IsConnected && _relay.Inputs[i].BooleanState;
        _prevStateMap.TryGetValue(tx, out bool wasActive);

        if (isActive && !wasActive)
        {
          int existingIndex = _activationHistory.IndexOf(tx);
          if (existingIndex >= 0)
          {
            _activationHistory.RemoveAt(existingIndex);
          }
          _activationHistory.Insert(0, tx);
          if (_activationHistory.Count > 8)
          {
            _activationHistory.RemoveAt(_activationHistory.Count - 1);
          }
        }

        _prevStateMap[tx] = isActive;
      }

      if (_relay.Mode == RelayMode.And)
      {
        bool allActive = true;
        Automator lastToTurnOn = null;
        for (int i = 0; i < inputCount; i++)
        {
          var tx = _relay.Inputs[i].Transmitter;
          bool isActive = _relay.Inputs[i].IsConnected && _relay.Inputs[i].BooleanState;
          if (!isActive) allActive = false;
          if (tx != null)
          {
            _prevStateMap.TryGetValue(tx, out bool wasActive);
            if (isActive && !wasActive) lastToTurnOn = tx;
          }
        }
        if (allActive && lastToTurnOn != null)
        {
          int idx = _activationHistory.IndexOf(lastToTurnOn);
          if (idx > 0)
          {
            _activationHistory.RemoveAt(idx);
            _activationHistory.Insert(0, lastToTurnOn);
          }
        }
      }

      Automator triggeringTransmitter = null;
      for (int i = 0; i < _activationHistory.Count; i++)
      {
        var candidate = _activationHistory[i];
        if (candidate == null) continue;
        for (int j = 0; j < inputCount; j++)
        {
          if (_relay.Inputs[j].Transmitter == candidate && _relay.Inputs[j].IsConnected && _relay.Inputs[j].BooleanState)
          {
            triggeringTransmitter = candidate;
            break;
          }
        }
        if (triggeringTransmitter != null) break;
      }

      if (triggeringTransmitter == null)
      {
        for (int j = 0; j < inputCount; j++)
        {
          if (_relay.Inputs[j].Transmitter != null && _relay.Inputs[j].IsConnected && _relay.Inputs[j].BooleanState)
          {
            triggeringTransmitter = _relay.Inputs[j].Transmitter;
            break;
          }
        }
      }

      if (_automator.UnfinishedState != AutomatorState.On)
      {
        return;
      }

      Color? finalColor = null;
      if (triggeringTransmitter != null)
      {
        var illum = triggeringTransmitter.GetComponent<CustomizableIlluminator>();
        if (illum != null) finalColor = illum.CustomColor;
      }

      if (finalColor.HasValue)
      {
        _customizableIlluminator.SetIsCustomized(true);
        _customizableIlluminator.SetCustomColor(finalColor.Value);
      }
    }
  }
}