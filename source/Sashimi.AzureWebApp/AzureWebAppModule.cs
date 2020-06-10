using Autofac;
using Sashimi.AzureWebApp.Endpoints;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Endpoints;

namespace Sashimi.AzureWebApp
{
    public class AzureWebAppModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AzureWebAppDeploymentTargetTypeProvider>().As<IDeploymentTargetTypeProvider>().SingleInstance();
            builder.RegisterType<AzureWebAppHealthCheckActionHandler>().As<IActionHandler>().AsSelf().InstancePerLifetimeScope();
        }
    }
}