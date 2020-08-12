using Autofac;
using Octopus.Extensibility.Actions.Sashimi;
using Octopus.Server.Extensibility.Extensions.Mappings;
using Sashimi.Azure.Accounts.Web;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Azure.Accounts
{
    public class AzureAccountModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AzureServicePrincipalAccountTypeProvider>().As<IAccountTypeProvider>().As<IContributeMappings>().SingleInstance();

            builder.RegisterType<AzureWebSitesListAction>().AsSelf().As<IAccountDetailsEndpoint>().InstancePerLifetimeScope();
            builder.RegisterType<AzureWebSitesSlotListAction>().AsSelf().As<IAccountDetailsEndpoint>().InstancePerLifetimeScope();
            builder.RegisterType<AzureResourceGroupsListAction>().AsSelf().As<IAccountDetailsEndpoint>().InstancePerLifetimeScope();
            builder.RegisterType<AzureStorageAccountsListAction>().AsSelf().As<IAccountDetailsEndpoint>().InstancePerLifetimeScope();
        }
    }
}