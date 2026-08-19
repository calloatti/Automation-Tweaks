using HarmonyLib;
using Timberborn.ModManagerScene;
using UnityEngine;

namespace Calloatti.AutoTweaks
{
  public class ModStarter : IModStarter
  {
    public void StartMod(IModEnvironment modEnvironment)
    {
      new Harmony("Calloatti.AutoTweaks").PatchAll();
      Debug.Log("[AutoTweaks] All Harmony patches applied successfully!");
    }
  }
}