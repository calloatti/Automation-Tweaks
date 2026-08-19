using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.Illumination;
using Timberborn.IlluminationUI;
using Timberborn.Localization;
using Calloatti.Loc;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.AutoTweaks
{
  public static class ColorNamesHelper
  {
    public static readonly HashSet<int> KnownColors = new HashSet<int>();
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
            KnownColors.Add(colorInt);
          }
        }
      }
      catch (Exception e) { Debug.LogError($"[AutoTweaks Load Error: {e.Message}"); }
    }
  }

  public sealed class BoolWrapper
  {
    public bool Value;
  }

  public sealed class UIPair
  {
    public Label ColorNameLabel;
    public Button ResetButton;
  }

  public static class ColorUIState
  {
    public static readonly ConditionalWeakTable<CustomizableIlluminator, BoolWrapper> PanelVisibility = new();
    public static readonly ConditionalWeakTable<CustomizableIlluminatorFragment, UIPair> FragmentUI = new();

    public static bool GetPanelVisible(CustomizableIlluminator illuminator)
    {
      return PanelVisibility.TryGetValue(illuminator, out var wrapper) ? wrapper.Value : false;
    }

    public static void SetPanelVisible(CustomizableIlluminator illuminator, bool visible)
    {
      var wrapper = PanelVisibility.GetOrCreateValue(illuminator);
      wrapper.Value = visible;
    }

    public static UIPair GetOrCreateFragmentUI(CustomizableIlluminatorFragment fragment, VisualElement root)
    {
      if (!FragmentUI.TryGetValue(fragment, out var pair))
      {
        var label = new Label(LocHolder.Instance.Loc.T("Calloatti.AutoTweaks.ColorUI.SelectedColor"))
        {
          style = { unityTextAlign = TextAnchor.MiddleCenter, color = new Color(0.7f, 0.7f, 0.7f), marginTop = 2, marginBottom = 2 }
        };
        var button = new Button();
        button.text = LocHolder.Instance.Loc.T("Calloatti.AutoTweaks.ColorUI.RevertToDefaultColor");

        Color mainBg = new Color32(45, 75, 60, 255);
        Color borderColor = new Color32(154, 134, 94, 255);
        Color textColor = new Color32(255, 255, 255, 255);

        button.style.backgroundColor = mainBg;
        button.style.color = textColor;

        button.style.borderTopColor = borderColor;
        button.style.borderBottomColor = borderColor;
        button.style.borderLeftColor = borderColor;
        button.style.borderRightColor = borderColor;

        button.style.borderTopWidth = 1;
        button.style.borderBottomWidth = 1;
        button.style.borderLeftWidth = 1;
        button.style.borderRightWidth = 1;

        button.style.borderTopLeftRadius = 1;
        button.style.borderTopRightRadius = 1;
        button.style.borderBottomLeftRadius = 1;
        button.style.borderBottomRightRadius = 1;

        button.style.marginTop = 10;
        button.style.marginBottom = 10;
        button.style.alignSelf = Align.Center;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.justifyContent = Justify.Center;
        button.style.width = new Length(90, LengthUnit.Percent);
        button.style.height = 24;

        var ui = new UIPair { ColorNameLabel = label, ResetButton = button };
        FragmentUI.Add(fragment, ui);
        return ui;
      }
      return FragmentUI.GetOrCreateValue(fragment);
    }
  }

  [HarmonyPatch(typeof(CustomizableIlluminator), "SetIsCustomized")]
  public static class Patch_SetIsCustomized
  {
    public static bool Prefix(CustomizableIlluminator __instance, bool value)
    {
      __instance.IsCustomized = true;
      return false;
    }
  }

  [HarmonyPatch(typeof(CustomizableIlluminator), "SetCustomColor")]
  public static class Patch_SetCustomColor
  {
    public static bool Prefix(CustomizableIlluminator __instance, Color? value)
    {
      if (value == null)
      {
        __instance._customColor = __instance._defaultColor;
        __instance.Apply();
        return false;
      }
      return true;
    }
  }

  [HarmonyPatch(typeof(CustomizableIlluminator), "Apply")]
  public static class Patch_Apply
{
        public static bool Prefix(CustomizableIlluminator __instance, IlluminatorColorizer ____illuminatorColorizer, ref Color? ____appliedColor, Color? ____customColor)
        {
          Color color = __instance._customColor ?? __instance._defaultColor;
          if (____appliedColor != color)
          {
            ____illuminatorColorizer.SetColor(color);
            ____appliedColor = color;
          }
          return false;
        }
      }

  [HarmonyPatch(typeof(CustomizableIlluminator), "get_EffectiveColor")]
  public static class Patch_EffectiveColor
  {
    public static bool Prefix(CustomizableIlluminator __instance, ref Color __result)
    {
      __result = __instance._customColor ?? __instance._defaultColor;
      return true;
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
        var ui = ColorUIState.GetOrCreateFragmentUI(__instance, __result);

        int insertIndex = rgbContainer.parent.IndexOf(rgbContainer);
        rgbContainer.parent.Insert(insertIndex + 1, ui.ColorNameLabel);
      }

      var uiPair = ColorUIState.GetOrCreateFragmentUI(__instance, __result);

      // Prevent duplicate callback registration
      if (!(uiPair.ResetButton.userData is bool))
      {
        uiPair.ResetButton.userData = true;
        uiPair.ResetButton.RegisterCallback<ClickEvent>(evt =>
        {
          var customIllum = __instance._customizableIlluminator;
          if (customIllum != null)
          {
            customIllum.SetCustomColor(null);
            Patch_CustomizableIlluminatorFragment_UpdateCustomColor.UpdateLabelText(__instance);
          }
        });
      }
      __result.Add(uiPair.ResetButton);

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
          if (ColorNamesHelper.KnownColors.Contains(colorKey))
          {
            string colorLocKey = $"Calloatti.AutoTweaks.ColorName.{colorKey:X6}";
            item2.RegisterCallback<MouseEnterEvent>(evt =>
            {
              if (ColorUIState.FragmentUI.TryGetValue(__instance, out var pair))
              {
                pair.ColorNameLabel.text = LocHolder.Instance.Loc.T(colorLocKey);
              }
            });
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
      if (!ColorUIState.FragmentUI.TryGetValue(__instance, out var pair) || __instance == null) return;

      var illuminator = __instance._customizableIlluminator;
      if (illuminator == null) return;

      if (pair.ResetButton != null)
      {
        Color? currentCustomColor = illuminator.CustomColor;
        pair.ResetButton.SetEnabled(currentCustomColor.HasValue);
      }

      Color32 c = illuminator.CustomColor;
      int key = (c.r << 16) | (c.g << 8) | c.b;
      string colorLocKey = $"Calloatti.AutoTweaks.ColorName.{key:X6}";
      pair.ColorNameLabel.text = ColorNamesHelper.KnownColors.Contains(key) ? LocHolder.Instance.Loc.T(colorLocKey) : LocHolder.Instance.Loc.T("Calloatti.AutoTweaks.ColorUI.CustomHexColor");
    }
  }

  [HarmonyPatch(typeof(CustomizableIlluminatorFragment), nameof(CustomizableIlluminatorFragment.UpdateFragment))]
  public static class Patch_CustomizableIlluminatorFragment_UpdateFragment
  {
    [HarmonyPostfix]
    public static void Postfix(CustomizableIlluminatorFragment __instance)
    {
      if (__instance._root == null) return;
      if (__instance._customizableIlluminator != null && __instance._customizableIlluminator.IsLocked)
      {
        __instance._root.style.display = DisplayStyle.None;
        return;
      }
      var illuminator = __instance._customizableIlluminator;
      bool visible = false;
      if (illuminator != null)
      {
        visible = ColorUIState.GetPanelVisible(illuminator);
      }
      __instance._root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
  }

  [HarmonyPatch(typeof(CustomizableIlluminatorFragment), nameof(CustomizableIlluminatorFragment.ClearFragment))]
  public static class Patch_CustomizableIlluminatorFragment_ClearFragment
  {
    [HarmonyPostfix]
    public static void Postfix(CustomizableIlluminatorFragment __instance)
    {
      if (__instance._customizableIlluminator != null)
      {
        ColorUIState.PanelVisibility.Remove(__instance._customizableIlluminator);
      }
    }
  }

  [HarmonyPatch(typeof(CustomizeIlluminationFragment), "OnClicked")]
  public static class Patch_CustomizeIlluminationFragment_OnClicked
  {
    [HarmonyPrefix]
    public static void Prefix(CustomizeIlluminationFragment __instance)
    {
      var illuminator = __instance._customizableIlluminator;
      if (illuminator != null)
      {
        bool current = ColorUIState.GetPanelVisible(illuminator);
        ColorUIState.SetPanelVisible(illuminator, !current);
      }
    }
  }
}