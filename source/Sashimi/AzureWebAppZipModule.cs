using Autofac;
using Octopus.Server.Extensibility.Extensions.Mappings;
using Sashimi.AzureWebAppZip;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Endpoints;

namespace Sashimi.AzureWebApp
{
       public class AzureWebAppZipModule : Module
       {
              protected override void Load(ContainerBuilder builder)
              {
                     builder.RegisterType<ActionHandler>()
                            .As<IActionHandler>()
                            .AsSelf()
                            .InstancePerLifetimeScope();
                     //builder.RegisterType<AzureWebAppScriptActionOverride>()
                     //       .As<IScriptActionOverride>()
                     //       .InstancePerLifetimeScope();
              }
       }
}