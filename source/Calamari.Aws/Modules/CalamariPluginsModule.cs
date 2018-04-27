using Autofac;
using Calamari.Aws.Integration;
using Calamari.Plugin;

namespace Calamari.Aws.Modules
{
    class CalamariPluginsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterAssemblyTypes(ThisAssembly).AssignableTo<AwsEnvironmentGeneration>().As<IScriptEnvironment>().SingleInstance();
        }
    }
}
