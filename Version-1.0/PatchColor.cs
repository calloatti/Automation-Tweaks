using HarmonyLib;
using Timberborn.Illumination;
using Timberborn.IlluminationUI;
using Timberborn.BaseComponentSystem;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace Calloatti.AutoTweaks
{
  public static class ColorNamesHelper
  {
    public static readonly Dictionary<int, string> ColorNames = new Dictionary<int, string>();
    private static bool _colorsLoaded = false;

    public static void LoadColorNamesFromText(string text)
    {
      if (_colorsLoaded) return;
      _colorsLoaded = true;
      try
      {
        string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
          if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || (line.StartsWith("#") && !line.Contains(","))) continue;
          string[] parts = line.Split(',');
          if (parts.Length >= 2 && int.TryParse(parts[0].Trim().TrimStart('#'), System.Globalization.NumberStyles.HexNumber, null, out int colorInt))
          {
            ColorNames[colorInt] = parts[1].Trim();
          }
        }
      }
      catch (Exception e) { Debug.LogError($"[AutoTweaks Load Error: {e.Message}"); }
    }
  }

  public static class ColorUIState
  {
    public static Label ColorNameLabel;
    public static Button ResetButton;
  }

  [HarmonyPatch(typeof(CustomizableIlluminator), "Apply")]
  public static class Patch_KeepColorWhenUIClosed
  {
    public static bool Prefix(CustomizableIlluminator __instance, IlluminatorColorizer ____illuminatorColorizer, ref Color? ____appliedColor, Color? ____customColor)
    {
      Color? colorToApply = ____customColor.HasValue ? ____customColor : null;
      if (____appliedColor != colorToApply)
      {
        if (colorToApply.HasValue) ____illuminatorColorizer.SetColor(colorToApply.Value);
        else ____illuminatorColorizer.ClearColor();
        ____appliedColor = colorToApply;

        // Note: Events are heavily restricted by the C# compiler even when publicized.
        // We maintain the reflection here specifically for the event invocation to ensure stability, 
        // while safely adding BindingFlags.Public to catch it if the publicizer shifted it.
        FieldInfo eventField = typeof(CustomizableIlluminator).GetField("AppliedColorChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (eventField != null)
        {
          MulticastDelegate eventDelegate = (MulticastDelegate)eventField.GetValue(__instance);
          if (eventDelegate != null)
          {
            foreach (var handler in eventDelegate.GetInvocationList())
            {
              handler.Method.Invoke(handler.Target, new object[] { __instance, EventArgs.Empty });
            }
          }
        }
      }
      return false;
    }
  }

  [HarmonyPatch(typeof(CustomizableIlluminatorFragment), nameof(CustomizableIlluminatorFragment.InitializeFragment))]
  public static class Patch_CustomizableIlluminatorFragment_InitializeFragment
  {
    [HarmonyPostfix]
    public static void Postfix(CustomizableIlluminatorFragment __instance, VisualElement __result)
    {
      var rgbField = __result.Q<TextField>("Rgb");
      if (rgbField != null)
      {
        rgbField.style.flexDirection = FlexDirection.Row;
        rgbField.style.justifyContent = Justify.Center;
        rgbField.style.alignItems = Align.Center;
        var internalLabel = rgbField.Q<Label>();
        if (internalLabel != null)
        {
          internalLabel.style.minWidth = StyleKeyword.Auto;
          internalLabel.style.width = StyleKeyword.Auto;
          internalLabel.style.marginRight = 5;
          internalLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        }
      }

      var rgbContainer = rgbField?.parent;
      if (rgbContainer != null && rgbContainer.parent != null)
      {
        ColorUIState.ColorNameLabel = new Label("Selected Color")
        {
          style = { unityTextAlign = TextAnchor.MiddleCenter, color = new Color(0.7f, 0.7f, 0.7f), marginTop = 2, marginBottom = 2 }
        };
        int insertIndex = rgbContainer.parent.IndexOf(rgbContainer);
        rgbContainer.parent.Insert(insertIndex + 1, ColorUIState.ColorNameLabel);
      }

      // 1. Create a standard Unity Button
      ColorUIState.ResetButton = new Button();
      ColorUIState.ResetButton.text = "Revert to Default Color";

      // 2. Simplified Colors (Using the previous hover color for the static background)
      Color mainBg = new Color32(45, 75, 60, 255);
      Color borderColor = new Color32(154, 134, 94, 255); // Game Gold
      Color textColor = new Color32(255, 255, 255, 255);  // White

      // 3. Apply Base Styles
      ColorUIState.ResetButton.style.backgroundColor = mainBg;
      ColorUIState.ResetButton.style.color = textColor;

      // Apply 1-pixel crisp borders
      ColorUIState.ResetButton.style.borderTopColor = borderColor;
      ColorUIState.ResetButton.style.borderBottomColor = borderColor;
      ColorUIState.ResetButton.style.borderLeftColor = borderColor;
      ColorUIState.ResetButton.style.borderRightColor = borderColor;

      ColorUIState.ResetButton.style.borderTopWidth = 1;
      ColorUIState.ResetButton.style.borderBottomWidth = 1;
      ColorUIState.ResetButton.style.borderLeftWidth = 1;
      ColorUIState.ResetButton.style.borderRightWidth = 1;

      ColorUIState.ResetButton.style.borderTopLeftRadius = 1;
      ColorUIState.ResetButton.style.borderTopRightRadius = 1;
      ColorUIState.ResetButton.style.borderBottomLeftRadius = 1;
      ColorUIState.ResetButton.style.borderBottomRightRadius = 1;

      // 4. Layout and Spacing
      ColorUIState.ResetButton.style.marginTop = 10;
      ColorUIState.ResetButton.style.marginBottom = 10;
      ColorUIState.ResetButton.style.alignSelf = Align.Center;
      ColorUIState.ResetButton.style.unityTextAlign = TextAnchor.MiddleCenter;
      ColorUIState.ResetButton.style.justifyContent = Justify.Center;
      ColorUIState.ResetButton.style.width = new Length(90, LengthUnit.Percent);
      ColorUIState.ResetButton.style.height = 24;

      ColorUIState.ResetButton.RegisterCallback<ClickEvent>(evt =>
      {
        var customIllum = __instance._customizableIlluminator;
        if (customIllum != null)
        {
          customIllum.SetCustomColor(null);
          Patch_CustomizableIlluminatorFragment_UpdateCustomColor.UpdateLabelText(__instance);
        }
      });
      __result.Add(ColorUIState.ResetButton);

      var list = __instance._presetColorButtons as System.Collections.IList;
      if (list != null)
      {
        foreach (object item in list)
        {
          var itemType = item.GetType();
          var item1 = AccessTools.Field(itemType, "Item1").GetValue(item);
          var item2 = (Button)AccessTools.Field(itemType, "Item2").GetValue(item);
          Color32 c32 = (Color)item1;
          int colorKey = (c32.r << 16) | (c32.g << 8) | (c32.b);
          if (ColorNamesHelper.ColorNames.TryGetValue(colorKey, out string name))
          {
            item2.RegisterCallback<MouseEnterEvent>(evt => { if (ColorUIState.ColorNameLabel != null) ColorUIState.ColorNameLabel.text = name; });
            item2.RegisterCallback<MouseLeaveEvent>(evt => Patch_CustomizableIlluminatorFragment_UpdateCustomColor.UpdateLabelText(__instance));
          }
        }
      }
    }
  }

  [HarmonyPatch(typeof(CustomizableIlluminatorFragment), nameof(CustomizableIlluminatorFragment.ShowFragment))]
  public static class Patch_CustomizableIlluminatorFragment_ShowFragment
  {
    [HarmonyPostfix]
    public static void Postfix(CustomizableIlluminatorFragment __instance)
    {
      Patch_CustomizableIlluminatorFragment_UpdateCustomColor.UpdateLabelText(__instance);
    }
  }

  [HarmonyPatch(typeof(CustomizableIlluminatorFragment), "UpdateCustomColor")]
  public static class Patch_CustomizableIlluminatorFragment_UpdateCustomColor
  {
    [HarmonyPostfix]
    public static void Postfix(CustomizableIlluminatorFragment __instance) => UpdateLabelText(__instance);

    public static void UpdateLabelText(CustomizableIlluminatorFragment __instance)
    {
      if (ColorUIState.ColorNameLabel == null || __instance == null) return;

      var illuminator = __instance._customizableIlluminator;
      if (illuminator == null) return;

      if (ColorUIState.ResetButton != null)
      {
        Color? currentCustomColor = illuminator.CustomColor;
        ColorUIState.ResetButton.SetEnabled(currentCustomColor.HasValue);
      }

      Color32 c = illuminator.CustomColor;
      int key = (c.r << 16) | (c.g << 8) | c.b;
      ColorUIState.ColorNameLabel.text = ColorNamesHelper.ColorNames.TryGetValue(key, out string name) ? name : "Custom Hex Color";
    }
  }
}