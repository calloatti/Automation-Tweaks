using System;
using HarmonyLib;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace Calloatti.AutoTweaks
{
  [HarmonyPatch(typeof(InputBoxShower.Builder), nameof(InputBoxShower.Builder.Show))]
  public static class PatchAutoRename
  {
    public static void Postfix(TextField ____input)
    {
      // Using Math.Max ensures we only ever increase the limit. 
      // If the vanilla game or another mod sets a limit higher than 64, 
      // we respectfully leave their higher limit intact.
      ____input.maxLength = Math.Max(____input.maxLength, 64);
    }
  }
}