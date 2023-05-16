using Autofac;
using Calamari.Common.Aws;

namespace Calamari.Aws.Integration
{
    public class CloudAccountsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AwsEnvironmentVariablesGenerator>().As<IAwsEnvironmentVariablesGenerator>();
        }
    }
}