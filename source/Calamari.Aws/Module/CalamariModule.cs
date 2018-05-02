using Autofac;
using Calamari.Hooks;

namespace Calamari.Aws.Module
{
    public class CalamariModule : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder
                .RegisterAssemblyTypes(ThisAssembly)
                .AssignableTo<IScriptWrapper>()
                .As<IScriptWrapper>()
                .SingleInstance();
        }
    }
}
