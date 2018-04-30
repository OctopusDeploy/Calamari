using Autofac;
using Calamari.Hooks;

namespace Calamari.Azure
{
    public class CalamariModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterAssemblyTypes(ThisAssembly).AssignableTo<IScriptWrapper>().As<IScriptWrapper>().SingleInstance();
        }
    }
}
