using Autofac;
using Calamari.Aws.Integration.Ecs;

namespace Calamari.Aws;

public class AwsModule: Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<EcsStackNameGenerator>().As<IEcsStackNameGenerator>().SingleInstance();
    }
}