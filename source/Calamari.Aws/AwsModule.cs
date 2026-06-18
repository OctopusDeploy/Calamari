using Autofac;
using Calamari.Aws.Discovery;
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
        builder.RegisterType<EcsDiscoverer>().As<IEcsDiscoverer>().InstancePerDependency();
        builder.RegisterType<AwsTargetDiscoveryContextResolver>().As<IAwsTargetDiscoveryContextResolver>().SingleInstance();
        builder.RegisterType<EcsClusterDiscoveryWriter>().As<IEcsClusterDiscoveryWriter>().InstancePerDependency();
    }
}