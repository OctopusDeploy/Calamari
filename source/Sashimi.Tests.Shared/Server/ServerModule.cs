using System;
using Autofac;

namespace Sashimi.Tests.Shared.Server
{
    public class ServerModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<TestCloudTemplateHandlerFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<TestFormatIdentifier>().AsImplementedInterfaces().SingleInstance();
        }
    }
}