using Autofac;
using Sashimi.AzureAppService;
using Sashimi.Server.Contracts.ActionHandlers;

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
              }
       }
}