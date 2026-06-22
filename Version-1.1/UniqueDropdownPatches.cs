using HarmonyLib;
using System;
using System.Collections.Generic;
using Timberborn.Automation;
using Timberborn.AutomationUI;
using Timberborn.BaseComponentSystem;
using Timberborn.InputSystem;
using Timberborn.SelectionSystem;
using UnityEngine;

namespace Calloatti.AutoTweaks
{
  [HarmonyPatch]
  public static class UniqueDropdownPatches
  {
    private static EntitySelectionService _selectionService;

    // 1. Capture the EntitySelectionService instance dynamically from the property getter
    [HarmonyPatch(typeof(EntitySelectionService), "IsAnythingSelected", MethodType.Getter)]
    [HarmonyPrefix]
    public static void IsAnythingSelected_Prefix(EntitySelectionService __instance)
    {
      _selectionService = __instance;
    }

    // 2. DROPDOWN FILTER: Removes already-selected transmitters from the UI choice array
    [HarmonyPatch(typeof(TransmitterDropdownProvider), "Items", MethodType.Getter)]
    [HarmonyPostfix]
    public static void Items_Getter_Postfix(TransmitterDropdownProvider __instance, ref IReadOnlyList<string> __result)
    {
      if (_selectionService == null || !_selectionService.IsAnythingSelected) return;

      var selectedEntity = _selectionService.SelectedObject;
      if (selectedEntity == null) return;

      var receiverAutomator = selectedEntity.GetComponent<Automator>();
      if (receiverAutomator == null) return;

      string currentSlotValue = __instance.GetValue();
      var forbiddenIds = new HashSet<string>();

      for (int i = 0; i < receiverAutomator.InputConnections.Count; i++)
      {
        var connection = receiverAutomator.InputConnections[i];
        if (connection.Transmitter != null)
        {
          string transmitterId = connection.Transmitter.AutomatorId;
          if (transmitterId != currentSlotValue)
          {
            forbiddenIds.Add(transmitterId);
          }
        }
      }

      if (forbiddenIds.Count == 0) return;

      var filteredList = new List<string>();
      foreach (var item in __result)
      {
        if (string.IsNullOrEmpty(item) || !forbiddenIds.Contains(item))
        {
          filteredList.Add(item);
        }
      }

      __result = filteredList.AsReadOnly();
    }

    // 3. MAP CLICK INTERCEPTION: Blocks selection if clicking an already-assigned building on the map
    [HarmonyPatch(typeof(TransmitterPickerTool), "ProcessInput")]
    [HarmonyPrefix]
    public static bool ProcessInput_Prefix(TransmitterPickerTool __instance, InputService ____inputService, BaseComponent ____owner)
    {
      // FIXED: Added input gates to ignore non-clicking frames, and removed sound controllers entirely
      if (____inputService == null || !____inputService.MainMouseButtonDown || ____inputService.MouseOverUI) return true;
      if (____owner == null) return true;

      var receiverAutomator = ____owner.GetComponent<Automator>();
      if (receiverAutomator == null) return true;

      // Use reflection-free tracking to see what building the 3D mouse raycast is currently targeting
      Automator hovered = Traverse.Create(__instance).Method("GetHoveredTransmitter").GetValue<Automator>();
      if (hovered == null) return true;

      // Scan our slots for a duplicate match
      for (int i = 0; i < receiverAutomator.InputConnections.Count; i++)
      {
        if (receiverAutomator.InputConnections[i].Transmitter == hovered)
        {
          // Intercept the click silently: stop selection processing without playing any sounds
          return false;
        }
      }
      return true;
    }

    // 4. MAP HIGHLIGHT OVERRIDE: Alters the visual highlight color to mark duplicate targets as invalid
    [HarmonyPatch(typeof(TransmitterPickerToolHighlighter), "HighlightTransmitter")]
    [HarmonyPrefix]
    public static bool HighlightTransmitter_Prefix(TransmitterPickerToolHighlighter __instance, Automator transmitter, BaseComponent ____owner, Highlighter ____highlighter)
    {
      if (____owner == null || transmitter == null) return true;

      var receiverAutomator = ____owner.GetComponent<Automator>();
      if (receiverAutomator == null) return true;

      // Check if this transmitter asset is already occupied by a different selection slot
      for (int i = 0; i < receiverAutomator.InputConnections.Count; i++)
      {
        if (receiverAutomator.InputConnections[i].Transmitter == transmitter)
        {
          // Force it to render an obvious, translucent warning red color on the map
          Color warningColor = new Color(0.8f, 0.1f, 0.1f, 0.5f);
          ____highlighter.HighlightPrimary(transmitter, warningColor);
          return false; // Skip vanilla coloring layout
        }
      }
      return true;
    }
  }
}