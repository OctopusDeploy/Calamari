using Autofac;
using Octopus.Server.Extensibility.Extensions.Mappings;
using Sashimi.GCP.Accounts.ControlTypes;
using Sashimi.GCP.Accounts.Variables;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.Actions.Templates;
using Sashimi.Server.Contracts.ServiceMessages;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.GCP.Accounts
{
    public class GcpAccountModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<GcpAccountTypeProvider>().As<IServiceMessageHandler>().As<IAccountTypeProvider>().As<IContributeMappings>().SingleInstance();
            builder.RegisterType<GcpVariableTypeProvider>().As<IVariableTypeProvider>().SingleInstance();
            builder.RegisterType<GcpControlTypeProvider>().As<IControlTypeProvider>().SingleInstance();
        }
    }
}