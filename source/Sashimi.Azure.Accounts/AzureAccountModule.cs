using System;
using Autofac;
using Octopus.Extensibility.Actions.Sashimi;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Octopus.Server.Extensibility.Extensions.Mappings;
using Octopus.Server.Extensibility.HostServices.Web;
using Sashimi.Azure.Accounts.Web;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Azure.Accounts
{
    public class AzureAccountModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AzureServicePrincipalAccountTypeProvider>().As<IAccountTypeProvider>().As<IContributeMappings>().SingleInstance();

            LoadWebSubModule(builder);
        }

        void LoadWebSubModule(ContainerBuilder builder)
        {
            builder.RegisterType<AzureApi>().As<RegistersEndpoints>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<AzureEnvironmentsListAction>().AsSelf().InstancePerDependency();
            builder.RegisterType<AzureHomeLinksContributor>().As<IHomeLinksContributor>().InstancePerDependency();

            builder.RegisterType<AzureWebSitesListAction>().AsSelf().As<IAccountDetailsEndpoint>().InstancePerLifetimeScope();
            builder.RegisterType<AzureWebSitesSlotListAction>().AsSelf().As<IAccountDetailsEndpoint>().InstancePerLifetimeScope();
            builder.RegisterType<AzureResourceGroupsListAction>().AsSelf().As<IAccountDetailsEndpoint>().InstancePerLifetimeScope();
            builder.RegisterType<AzureStorageAccountsListAction>().AsSelf().As<IAccountDetailsEndpoint>().InstancePerLifetimeScope();
        }
    }
}