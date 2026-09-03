using Bindito.Core;
using Timberborn.AutomationBuildings;
using Timberborn.TemplateInstantiation;

namespace Calloatti.AutoTweaks
{
  [Context("Game")]
  public class MemoryColorReplicatorConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<MemoryColorReplicator>().AsTransient();

      MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
    }

    private static TemplateModule ProvideTemplateModule()
    {
      TemplateModule.Builder builder = new TemplateModule.Builder();

      builder.AddDecorator<Memory, MemoryColorReplicator>();

      return builder.Build();
    }
  }
}
