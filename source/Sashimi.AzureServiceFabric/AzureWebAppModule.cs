using Autofac;
using Sashimi.AzureServiceFabric.Endpoints;
using Sashimi.Server.Contracts.Endpoints;

namespace Sashimi.AzureServiceFabric
{
    public class AzureServiceFabricModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AzureServiceFabricClusterDeploymentTargetTypeProvider>().As<IDeploymentTargetTypeProvider>().SingleInstance();
        }
    }
}