using Autofac;
using Octopus.Server.Extensibility.Extensions.Mappings;
using Sashimi.GoogleCloud.Accounts.ControlTypes;
using Sashimi.GoogleCloud.Accounts.Variables;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.Actions.Templates;
using Sashimi.Server.Contracts.ServiceMessages;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.GoogleCloud.Accounts
{
    public class GoogleCloudAccountModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<GoogleCloudAccountTypeProvider>().As<IAccountTypeProvider>().As<IContributeMappings>().SingleInstance();
        }
    }
}