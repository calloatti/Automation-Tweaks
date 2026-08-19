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

    private readonly List<Automator> _activationHistory = new List<Automator>(2);

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
      if (_relay.InputA != null)
      {
        currentTransmitters.Add(_relay.InputA);
        var illum = _relay.InputA.GetComponent<CustomizableIlluminator>();
        if (illum != null && illum)
        {
          illum.CustomColorChanged += OnInputCustomColorChanged;
          _subscribedIlluminators.Add(illum);
        }
      }

      if (_relay.UsesInputB && _relay.InputB != null)
      {
        currentTransmitters.Add(_relay.InputB);
        var illum = _relay.InputB.GetComponent<CustomizableIlluminator>();
        if (illum != null && illum)
        {
          illum.CustomColorChanged += OnInputCustomColorChanged;
          _subscribedIlluminators.Add(illum);
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
      bool aActive = _relay.InputA != null && _relay.InputA.State == AutomatorState.On;
      bool bActive = _relay.UsesInputB && _relay.InputB != null && _relay.InputB.State == AutomatorState.On;

      if (_relay.InputA != null && aActive)
      {
        _activationHistory.Add(_relay.InputA);
      }
      if (_relay.InputB != null && bActive)
      {
        _activationHistory.Add(_relay.InputB);
      }
    }

    private void ReplicateInputColors()
    {
      if (!IsColorReplicationEnabled) return;

      bool aActive = _relay.InputA != null && _relay.InputA.State == AutomatorState.On;
      bool bActive = _relay.UsesInputB && _relay.InputB != null && _relay.InputB.State == AutomatorState.On;

      if (_relay.InputA != null)
      {
        _prevStateMap.TryGetValue(_relay.InputA, out bool wasActive);
        if (aActive && !wasActive)
        {
          int existingIndex = _activationHistory.IndexOf(_relay.InputA);
          if (existingIndex >= 0) _activationHistory.RemoveAt(existingIndex);
          _activationHistory.Insert(0, _relay.InputA);
          if (_activationHistory.Count > 2) _activationHistory.RemoveAt(_activationHistory.Count - 1);
        }
        _prevStateMap[_relay.InputA] = aActive;
      }
      if (_relay.InputB != null)
      {
        _prevStateMap.TryGetValue(_relay.InputB, out bool wasActive);
        if (bActive && !wasActive)
        {
          int existingIndex = _activationHistory.IndexOf(_relay.InputB);
          if (existingIndex >= 0) _activationHistory.RemoveAt(existingIndex);
          _activationHistory.Insert(0, _relay.InputB);
          if (_activationHistory.Count > 2) _activationHistory.RemoveAt(_activationHistory.Count - 1);
        }
        _prevStateMap[_relay.InputB] = bActive;
      }

      if (_relay.Mode == RelayMode.And)
      {
        if (aActive && bActive)
        {
          Automator lastToTurnOn = null;
          if (_relay.InputA != null && _prevStateMap.TryGetValue(_relay.InputA, out bool aWasActive) && aActive && !aWasActive)
            lastToTurnOn = _relay.InputA;
          if (_relay.InputB != null && _prevStateMap.TryGetValue(_relay.InputB, out bool bWasActive) && bActive && !bWasActive)
            lastToTurnOn = _relay.InputB;

          if (lastToTurnOn != null)
          {
            int idx = _activationHistory.IndexOf(lastToTurnOn);
            if (idx > 0)
            {
              _activationHistory.RemoveAt(idx);
              _activationHistory.Insert(0, lastToTurnOn);
            }
          }
        }
      }

      Automator triggeringTransmitter = null;
      for (int i = 0; i < _activationHistory.Count; i++)
      {
        var candidate = _activationHistory[i];
        if (candidate == _relay.InputA && aActive)
        {
          triggeringTransmitter = candidate;
          break;
        }
        if (candidate == _relay.InputB && bActive)
        {
          triggeringTransmitter = candidate;
          break;
        }
      }

      if (triggeringTransmitter == null)
      {
        if (aActive && _relay.InputA != null) triggeringTransmitter = _relay.InputA;
        else if (bActive && _relay.InputB != null) triggeringTransmitter = _relay.InputB;
      }

      if (_automator.UnfinishedState != AutomatorState.On)
      {
        return;
      }

      Color? finalColor = null;
      if (triggeringTransmitter != null)
      {
        if (triggeringTransmitter == _relay.InputA && _relay.InputA != null)
        {
          var illum = _relay.InputA.GetComponent<CustomizableIlluminator>();
          if (illum != null) finalColor = illum.CustomColor;
        }
        else if (triggeringTransmitter == _relay.InputB && _relay.InputB != null)
        {
          var illum = _relay.InputB.GetComponent<CustomizableIlluminator>();
          if (illum != null) finalColor = illum.CustomColor;
        }
      }

      if (finalColor.HasValue)
      {
        _customizableIlluminator.SetIsCustomized(true);
        _customizableIlluminator.SetCustomColor(finalColor.Value);
      }
    }
  }
}