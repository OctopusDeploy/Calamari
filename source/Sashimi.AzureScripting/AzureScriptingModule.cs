using Autofac;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureScripting
{
       public class AzureScriptingModule : Module
       {
              protected override void Load(ContainerBuilder builder)
              {
                     builder.RegisterType<AzurePowerShellActionHandler>()
                            .As<IActionHandler>()
                            .AsSelf()
                            .InstancePerLifetimeScope();
              }
       }
}