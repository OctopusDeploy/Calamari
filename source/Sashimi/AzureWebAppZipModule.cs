using Autofac;
using Octopus.Server.Extensibility.Extensions.Mappings;
using Sashimi.AzureAppService;
using Sashimi.AzureAppService.Endpoints;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.CloudTemplates;
using Sashimi.Server.Contracts.Endpoints;

namespace Sashimi.AzureWebApp
{
       public class AzureAppServiceModule : Module
       {
              protected override void Load(ContainerBuilder builder)
              {
                     builder.RegisterType<AzureWebAppDeploymentTargetTypeProvider>()
                            .As<IDeploymentTargetTypeProvider>()
                            .As<IContributeMappings>()
                            .SingleInstance();
                     builder.RegisterType<AzureWebAppHealthCheckActionHandler>()
                            .As<IActionHandler>()
                            .AsSelf()
                            .InstancePerLifetimeScope();
                     builder.RegisterType<AzureAppServiceActionHandler>()
                            .As<IActionHandler>()
                            .AsSelf()
                            .InstancePerLifetimeScope();
                     builder.RegisterType<AzureWebAppServiceMessageHandler>()
                            .AsSelf()
                            .InstancePerLifetimeScope();
                     builder.RegisterType<AzureCloudTemplateHandler>()
                            .As<ICloudTemplateHandler>()
                            .SingleInstance();
              }
       }
}