using Autofac;
using Calamari.Common.Aws;

namespace Calamari.CloudAccounts
{
    public class CloudAccountsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AwsEnvironmentVariablesFactory>().As<IAwsEnvironmentVariablesFactory>();
        }
    }
}