using HarmonyLib;
using Timberborn.Automation;
using Timberborn.AutomationBuildings;
using Timberborn.Buildings;

namespace AutomationPausableMod
{
  /// <summary>
  /// Enables the Pause UI fragment by returning true for transmitters.
  /// </summary>
  [HarmonyPatch(typeof(PausableBuilding), nameof(PausableBuilding.IsPausable))]
  public static class Patch_PausableBuilding_IsPausable
  {
    public static void Postfix(PausableBuilding __instance, ref bool __result)
    {
      if (__result) return;

      Automator automator = __instance.GetComponent<Automator>();
      if (automator != null && automator.IsTransmitter)
      {
        __result = true;
      }
    }
  }

  /// <summary>
  /// Intercepts ANY attempt by the building to change its network state.
  /// If the building is paused, we strictly force the new state to be 'On'.
  /// This fixes the load issues, JIT inlining, and UI desyncs all at once because 
  /// the literal underlying memory field will be set to On.
  /// </summary>
  [HarmonyPatch(typeof(Automator), "SetStateInternal")]
  public static class Patch_Automator_SetStateInternal
  {
    public static void Prefix(Automator __instance, ref AutomatorState newState)
    {
      PausableBuilding pausable = __instance.GetComponent<PausableBuilding>();
      if (pausable != null && pausable.Paused)
      {
        newState = AutomatorState.On;
      }
    }
  }

  /// <summary>
  /// Instantly pushes the 'On' state to the network the moment you click Pause.
  /// </summary>
  [HarmonyPatch(typeof(PausableBuilding), nameof(PausableBuilding.Pause))]
  public static class Patch_PausableBuilding_Pause
  {
    public static void Postfix(PausableBuilding __instance)
    {
      Automator automator = __instance.GetComponent<Automator>();
      if (automator != null && automator.IsTransmitter)
      {
        automator.SetState(true);
      }
    }
  }

  /// <summary>
  /// Restores the true, calculated state to the network the moment you click Resume.
  /// </summary>
  [HarmonyPatch(typeof(PausableBuilding), nameof(PausableBuilding.Resume))]
  public static class Patch_PausableBuilding_Resume
  {
    public static void Postfix(PausableBuilding __instance)
    {
      Automator automator = __instance.GetComponent<Automator>();
      if (automator != null && automator.IsTransmitter)
      {
        // Force the building to recalculate and apply its TRUE state now that it is unpaused
        if (automator.IsSamplingTransmitter)
        {
          Traverse.Create(automator).Method("Sample").GetValue();
        }
        else if (automator.IsCombinationalTransmitter)
        {
          Traverse.Create(automator).Method("EvaluateCombinational").GetValue();
        }
        else
        {
          // Handle Levers (which are manual, not sampling/combinational)
          Lever lever = __instance.GetComponent<Lever>();
          if (lever != null)
          {
            automator.SetState(lever.IsOn);
          }
        }
      }
    }
  }
}