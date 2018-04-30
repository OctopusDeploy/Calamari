using Autofac;
using Calamari.Hooks;

namespace Calamari.Aws
{
    public class CalamariModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder
                .RegisterAssemblyTypes(ThisAssembly)
                .AssignableTo<IScriptEnvironment>()
                .As<IScriptEnvironment>()
                .SingleInstance();
        }
    }
}
