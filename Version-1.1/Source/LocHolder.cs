using Bindito.Core;
using Timberborn.Localization;
using Timberborn.SingletonSystem;

namespace Calloatti.Loc
{
  [Context("Game")]
  [Context("MapEditor")]
  public class LocHolderConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<LocHolder>().AsSingleton();
    }
  }

  public class LocHolder : ILoadableSingleton
  {
    public static LocHolder Instance { get; private set; }

    public ILoc Loc { get; }

    public void Load()
    {
    }

    public LocHolder(ILoc loc)
    {
      Instance = this;
      Loc = loc;
    }
  }
}