using HarmonyLib;
using Timberborn.CoreUI;
using Timberborn.AutomationUI;
using UnityEngine.UIElements;

namespace Calloatti.AutoTweaks
{
  [HarmonyPatch(typeof(VisualElementInitializer), "InitializeVisualElement")]
  public static class Patch_WidenAutomationUIAndReduceFont
  {
    static void Postfix(VisualElement visualElement)
    {
      // 1. Widen the Selector Wrapper
      if (visualElement is TransmitterSelector selector)
      {
        // Force width to 100% to overpower the game's "transmitter-selector--automatable-none" class
        selector.style.width = new Length(100, LengthUnit.Percent);
        selector.style.marginLeft = 0;

        // 2. Widen the Dropdown
        var dropdown = selector.Q("TransmitterDropdown");
        if (dropdown != null)
        {
          // Let the dropdown aggressively consume space up to the light
          dropdown.style.flexGrow = 1;

          // Remove any invisible maximum width barriers
          dropdown.style.maxWidth = new StyleLength(StyleKeyword.None);
          dropdown.style.marginLeft = 0;
        }
      }

      // 3. Dropdown Row Font Size Reduction
      if (visualElement.ClassListContains("dropdown-item__text") && visualElement is Label label)
      {
        label.RegisterCallback<GeometryChangedEvent>(evt =>
        {
          float currentSize = label.resolvedStyle.fontSize;
          if (currentSize > 0 && label.style.fontSize.keyword == StyleKeyword.Null)
          {
            label.style.fontSize = currentSize - 1f;
          }
        });
      }
    }
  }
}