using Autofac;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureResourceGroup
{
       public class AzureResourceGroupModule : Module
       {
              protected override void Load(ContainerBuilder builder)
              {
                     builder.RegisterType<AzureResourceGroupActionHandler>()
                            .As<IActionHandler>()
                            .AsSelf()
                            .InstancePerLifetimeScope();
              }
       }
}