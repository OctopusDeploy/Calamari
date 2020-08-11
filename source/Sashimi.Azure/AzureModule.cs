using Autofac;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Octopus.Server.Extensibility.HostServices.Web;
using Sashimi.Azure.Web;

namespace Sashimi.Azure
{
    public class AzureModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            LoadWebSubModule(builder);   
        }

        void LoadWebSubModule(ContainerBuilder builder)
        {
            builder.RegisterType<AzureApi>().As<RegistersEndpoints>().AsSelf().InstancePerLifetimeScope();

            builder.RegisterType<AzureEnvironmentsListAction>().AsSelf().InstancePerDependency();

            builder.RegisterType<AzureHomeLinksContributor>().As<IHomeLinksContributor>().InstancePerDependency();
        }
    }
}