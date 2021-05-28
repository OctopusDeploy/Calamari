using Autofac;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.GoogleCloud.Scripting
{
    public class GoogleCloudScriptingModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<GoogleCloudActionHandler>()
                .As<IActionHandler>()
                .AsSelf()
                .InstancePerLifetimeScope();
        }
    }
}