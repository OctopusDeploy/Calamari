using Autofac;
using Calamari.Aws.Inputs.Ecs;
using Calamari.Aws.Integration.Ecs;

namespace Calamari.Aws;

public class AwsModule: Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<EcsStackNameGenerator>().As<IEcsStackNameGenerator>().SingleInstance();
        builder.RegisterType<EcsImageNameResolver>().As<IEcsImageNameResolver>().SingleInstance();
        
        builder.RegisterType<EcsClientFactory>().As<IEcsClientFactory>().SingleInstance();
    }
}