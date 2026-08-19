using Bindito.Core;
using UnityEngine;
using Timberborn.AssetSystem;
using Timberborn.SingletonSystem;

namespace Calloatti.AutoTweaks
{
  [Context("Game")]
  [Context("MapEditor")]
  internal class ColorNamesConfigurator : IConfigurator
  {
    public void Configure(IContainerDefinition containerDefinition)
    {
      containerDefinition.Bind<ColorNamesLoader>().AsSingleton();
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
      var textAsset = _assetLoader.LoadSafe<TextAsset>("resources/autotweaks.colornames");

      if (textAsset != null)
      {
        // Notice we call the new helper class here!
        ColorNamesHelper.LoadColorNamesFromText(textAsset.text);
      }
      else
      {
        Debug.LogWarning("[AutoTweaks] Could not find 'resources/autotweaks.colornames' text asset.");
      }
    }
  }
}