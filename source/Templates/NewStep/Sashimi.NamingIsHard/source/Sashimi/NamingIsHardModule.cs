using System;
using Autofac;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.NamingIsHard
{
    public class NamingIsHardModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<MyActionHandler>()
                   .As<IActionHandler>()
                   .AsSelf()
                   .InstancePerLifetimeScope();
        }
    }
}