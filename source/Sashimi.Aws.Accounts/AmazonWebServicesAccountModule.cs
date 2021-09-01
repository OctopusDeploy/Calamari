using System;
using Autofac;
using Octopus.Server.Extensibility.Extensions.Mappings;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Aws.Accounts
{
    public class AmazonWebServicesAccountModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AwsHttpClientFactory>().AsSelf().SingleInstance();
            builder.RegisterType<AmazonWebServicesAccountVerifier>().AsSelf().SingleInstance();

            builder.RegisterType<AmazonWebServicesAccountTypeProvider>().As<IAccountTypeProvider>().As<IContributeMappings>().SingleInstance();
        }
    }
}