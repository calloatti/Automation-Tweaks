using Bindito.Core;
using HarmonyLib;
using UnityEngine;
using Timberborn.AssetSystem;
using Timberborn.SingletonSystem;

namespace Calloatti.AutoTweaks
{
  [Context("Game")]
  [Context("MapEditor")]
  internal class PatchConfigurator : IConfigurator
  {
    private const string HarmonyId = "calloatti.autotools";
    private static Harmony _harmony;

    public void Configure(IContainerDefinition containerDefinition)
    {
      containerDefinition.Bind<ColorNamesLoader>().AsSingleton();

      if (_harmony == null)
      {
        _harmony = new Harmony(HarmonyId);
        _harmony.PatchAll(typeof(PatchConfigurator).Assembly); // This now auto-loads all patches in PatchColor.cs!
        Debug.Log($"[{HarmonyId}] All Harmony patches applied successfully!");
      }
    }
  }

  internal class ColorNamesLoader : ILoadableSingleton
  {
    private readonly IAssetLoader _assetLoader;

    public ColorNamesLoader(IAssetLoader assetLoader)
    {
      _assetLoader = assetLoader;
    }

    public void Load()
    {
      var textAsset = _assetLoader.LoadSafe<TextAsset>("resources/autotools.colornames");

      if (textAsset != null)
      {
        // Notice we call the new helper class here!
        ColorNamesHelper.LoadColorNamesFromText(textAsset.text);
      }
      else
      {
        Debug.LogWarning("[AutoTweaks] Could not find 'resources/autotools.colornames' text asset.");
      }
    }
  }
}