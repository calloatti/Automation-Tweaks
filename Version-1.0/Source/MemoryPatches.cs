using HarmonyLib;
using Timberborn.AutomationBuildings;
using Timberborn.AutomationBuildingsUI;
using Timberborn.BaseComponentSystem;
using Timberborn.Localization;
using Calloatti.Loc;
using UnityEngine.UIElements;

namespace Calloatti.AutoTweaks
{
  public static class MemoryUIState
  {
    public static Toggle ColorReplicationToggle;
    public static MemoryColorReplicator CurrentReplicator;
  }

  [HarmonyPatch(typeof(MemoryFragment), nameof(MemoryFragment.InitializeFragment))]
  public static class Patch_MemoryFragment_InitializeFragment
  {
    [HarmonyPostfix]
    public static void Postfix(VisualElement __result)
    {
      MemoryUIState.ColorReplicationToggle = new Toggle();
      MemoryUIState.ColorReplicationToggle.text = LocHolder.Instance.Loc.T("Building.Indicator.ReplicateInputColor");

      MemoryUIState.ColorReplicationToggle.AddToClassList("game-toggle");
      MemoryUIState.ColorReplicationToggle.AddToClassList("entity-panel__text");
      MemoryUIState.ColorReplicationToggle.AddToClassList("entity-panel__toggle");

      MemoryUIState.ColorReplicationToggle.RegisterValueChangedCallback(evt =>
      {
        if (MemoryUIState.CurrentReplicator != null)
        {
          MemoryUIState.CurrentReplicator.SetColorReplicationEnabled(evt.newValue);
        }
      });

      __result.Add(MemoryUIState.ColorReplicationToggle);
    }
  }

  [HarmonyPatch(typeof(MemoryFragment), nameof(MemoryFragment.ShowFragment))]
  public static class Patch_MemoryFragment_ShowFragment
  {
    [HarmonyPostfix]
    public static void Postfix(BaseComponent entity)
    {
      if (entity != null)
      {
        MemoryUIState.CurrentReplicator = entity.GetComponent<MemoryColorReplicator>();
      }
    }
  }

  [HarmonyPatch(typeof(MemoryFragment), nameof(MemoryFragment.UpdateFragment))]
  public static class Patch_MemoryFragment_UpdateFragment
  {
    [HarmonyPostfix]
    public static void Postfix()
    {
      if (MemoryUIState.CurrentReplicator != null && MemoryUIState.ColorReplicationToggle != null)
      {
        MemoryUIState.ColorReplicationToggle.SetValueWithoutNotify(MemoryUIState.CurrentReplicator.IsColorReplicationEnabled);
      }
    }
  }

  [HarmonyPatch(typeof(MemoryFragment), nameof(MemoryFragment.ClearFragment))]
  public static class Patch_MemoryFragment_ClearFragment
  {
    [HarmonyPostfix]
    public static void Postfix()
    {
      MemoryUIState.CurrentReplicator = null;
    }
  }

  [HarmonyPatch(typeof(Memory), nameof(Memory.CommitTick))]
  public static class Patch_Memory_CommitTick
  {
    [HarmonyPostfix]
    public static void Postfix(Memory __instance)
    {
      MemoryColorReplicator replicator = __instance.GetComponent<MemoryColorReplicator>();
      if (replicator != null)
      {
        replicator.EvaluateColors();
      }
    }
  }
}
