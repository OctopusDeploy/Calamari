using Autofac;
using Calamari.Common.Aws;

namespace Calamari.Aws.Integration
{
    public class CloudAccountsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AwsEnvironmentVariablesFactory>().As<IAwsEnvironmentVariablesFactory>();
        }
    }
}