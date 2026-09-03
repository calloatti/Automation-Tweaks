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
  public class MemoryColorReplicator : BaseComponent, IAwakableComponent, IPersistentEntity, IFinishedStateListener
  {
    private static readonly ComponentKey ReplicatorKey = new ComponentKey("MemoryColorReplicator");
    private static readonly PropertyKey<bool> IsEnabledKey = new PropertyKey<bool>("IsColorReplicationEnabled");

    private Memory _memory;
    private Automator _automator;
    private CustomizableIlluminator _customizableIlluminator;

    private readonly List<CustomizableIlluminator> _subscribedIlluminators = new List<CustomizableIlluminator>();

    private readonly List<Automator> _activationHistory = new List<Automator>(2);

    private readonly Dictionary<Automator, bool> _prevStateMap = new Dictionary<Automator, bool>();

    private Automator _lastInputA;
    private Automator _lastInputB;

    public bool IsColorReplicationEnabled { get; private set; }

    public void Awake()
    {
      _memory = GetComponent<Memory>();
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
      _lastInputA = _memory.InputA;
      _lastInputB = _memory.InputB;
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
        CheckForInputChanges();
        ReplicateInputColors();
      }
    }

    private void CheckForInputChanges()
    {
      bool inputAChanged = _memory.InputA != _lastInputA;
      bool inputBChanged = _memory.InputB != _lastInputB;

      if (inputAChanged || inputBChanged)
      {
        _lastInputA = _memory.InputA;
        _lastInputB = _memory.InputB;
        ResubscribeToInputColors();
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

      AddTransmitterIfValid(_memory.InputA, currentTransmitters);
      if (_memory.UsesInputB)
      {
        AddTransmitterIfValid(_memory.InputB, currentTransmitters);
      }

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

    private void AddTransmitterIfValid(Automator transmitter, HashSet<Automator> currentTransmitters)
    {
      if (transmitter == null) return;
      currentTransmitters.Add(transmitter);
      var illum = transmitter.GetComponent<CustomizableIlluminator>();
      if (illum != null && illum)
      {
        illum.CustomColorChanged += OnInputCustomColorChanged;
        _subscribedIlluminators.Add(illum);
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

      var inputA = _memory.InputA;
      if (inputA != null && _automator.InputConnections[0].IsConnected && _automator.InputConnections[0].BooleanState)
      {
        _activationHistory.Add(inputA);
      }

      if (_memory.UsesInputB)
      {
        var inputB = _memory.InputB;
        if (inputB != null && _automator.InputConnections[1].IsConnected && _automator.InputConnections[1].BooleanState)
        {
          _activationHistory.Add(inputB);
        }
      }
    }

    private void ReplicateInputColors()
    {
      if (!IsColorReplicationEnabled) return;

      UpdateActivationHistory();

      Automator triggeringTransmitter = FindTriggeringTransmitter();

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

    private void UpdateActivationHistory()
    {
      CheckAndTrackInput(_memory.InputA, 0);
      if (_memory.UsesInputB)
      {
        CheckAndTrackInput(_memory.InputB, 1);
      }
    }

    private void CheckAndTrackInput(Automator transmitter, int inputIndex)
    {
      if (transmitter == null) return;

      bool isActive = _automator.InputConnections[inputIndex].IsConnected && _automator.InputConnections[inputIndex].BooleanState;
      _prevStateMap.TryGetValue(transmitter, out bool wasActive);

      if (isActive && !wasActive)
      {
        int existingIndex = _activationHistory.IndexOf(transmitter);
        if (existingIndex >= 0)
        {
          _activationHistory.RemoveAt(existingIndex);
        }
        _activationHistory.Insert(0, transmitter);
        if (_activationHistory.Count > 2)
        {
          _activationHistory.RemoveAt(_activationHistory.Count - 1);
        }
      }

      _prevStateMap[transmitter] = isActive;
    }

    private Automator FindTriggeringTransmitter()
    {
      for (int i = 0; i < _activationHistory.Count; i++)
      {
        var candidate = _activationHistory[i];
        if (candidate == null) continue;

        if (IsInputActiveAndConnected(candidate))
        {
          return candidate;
        }
      }

      return FindFirstActiveInput();
    }

    private bool IsInputActiveAndConnected(Automator transmitter)
    {
      if (_memory.InputA == transmitter && _automator.InputConnections[0].IsConnected && _automator.InputConnections[0].BooleanState)
      {
        return true;
      }
      if (_memory.UsesInputB && _memory.InputB == transmitter && _automator.InputConnections[1].IsConnected && _automator.InputConnections[1].BooleanState)
      {
        return true;
      }
      return false;
    }

    private Automator FindFirstActiveInput()
    {
      if (_automator.InputConnections[0].IsConnected && _automator.InputConnections[0].BooleanState)
      {
        return _memory.InputA;
      }
      if (_memory.UsesInputB && _automator.InputConnections[1].IsConnected && _automator.InputConnections[1].BooleanState)
      {
        return _memory.InputB;
      }
      return null;
    }
  }
}
