using Autofac;
using Octopus.Server.Extensibility.Extensions.Mappings;
using Sashimi.AzureAppService;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Endpoints;

namespace Sashimi.AzureWebApp
{
       public class AzureAppServiceModule : Module
       {
              protected override void Load(ContainerBuilder builder)
              {
                     builder.RegisterType<AzureAppServiceActionHandler>()
                            .As<IActionHandler>()
                            .AsSelf()
                            .InstancePerLifetimeScope();
                     //builder.RegisterType<AzureWebAppScriptActionOverride>()
                     //       .As<IScriptActionOverride>()
                     //       .InstancePerLifetimeScope();
              }
       }
}