using Autofac;
using Octopus.Server.Extensibility.Extensions.Mappings;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Endpoints;

namespace Sashimi.AzureWebApp
{
       public class AzureWebAppModule : Module
       {
              protected override void Load(ContainerBuilder builder)
              {
                     builder.RegisterType<AzureWebAppActionHandler>()
                            .As<IActionHandler>()
                            .AsSelf()
                            .InstancePerLifetimeScope();
                     builder.RegisterType<AzureWebAppPackageContributor>()
                            .As<IContributeToPackageDeployment>()
                            .InstancePerLifetimeScope();
                     builder.RegisterType<AzureWebAppScriptActionOverride>()
                            .As<IScriptActionOverride>()
                            .InstancePerLifetimeScope();
              }
       }
}