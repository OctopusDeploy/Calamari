using Autofac;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Aws.Accounts
{
    public class AmazonWebServicesAccountModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AmazonWebServicesAccountTypeProvider>().As<IAccountTypeProvider>().SingleInstance();
        }
    }
}