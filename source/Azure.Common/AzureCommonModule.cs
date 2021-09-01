using System;
using Autofac;
using Sashimi.Azure.Common.ControlTypes;
using Sashimi.Azure.Common.Variables;
using Sashimi.Server.Contracts.Actions.Templates;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Azure.Common
{
    public class AzureCommonModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AzureVariableTypeProvider>().As<IVariableTypeProvider>().SingleInstance();
            builder.RegisterType<AzureControlTypeProvider>().As<IControlTypeProvider>().SingleInstance();
        }
    }
}