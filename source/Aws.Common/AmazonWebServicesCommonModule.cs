using System;
using Autofac;
using Sashimi.Aws.Common.ControlTypes;
using Sashimi.Aws.Common.Variables;
using Sashimi.Server.Contracts.Actions.Templates;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Aws.Common
{
    public class AmazonWebServicesCommonModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AmazonWebServicesVariableTypeProvider>().As<IVariableTypeProvider>().SingleInstance();
            builder.RegisterType<AmazonWebServicesControlTypeProvider>().As<IControlTypeProvider>().SingleInstance();
        }
    }
}