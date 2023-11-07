using Autofac;

namespace Calamari.CloudAccounts
{
    public class CloudAccountsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AzureAuthTokenService>().As<IAzureAuthTokenService>().InstancePerDependency();
            builder.RegisterType<AzureClientFactory>().As<IAzureClientFactory>().InstancePerDependency();
        }
    }
}