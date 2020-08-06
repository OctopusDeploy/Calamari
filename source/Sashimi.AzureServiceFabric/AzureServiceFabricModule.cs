using Autofac;
using Octopus.Server.Extensibility.Extensions.Mappings;
using Sashimi.AzureServiceFabric.Endpoints;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Endpoints;

namespace Sashimi.AzureServiceFabric
{
    public class AzureServiceFabricModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AzureServiceFabricClusterDeploymentTargetTypeProvider>()
                .As<IDeploymentTargetTypeProvider>()
                .As<IContributeMappings>()
                .SingleInstance();
            builder.RegisterType<AzureServiceFabricAppHealthCheckActionHandler>().As<IActionHandler>().AsSelf()
                .InstancePerLifetimeScope();
        }
    }
}