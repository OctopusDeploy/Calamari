using Autofac;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Integration
{
    public class CloudAccountsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AwsAuthConventionFactory>().As<IAwsAuthConventionFactory>();
        }
    }
}