using Autofac;
using Calamari.Hooks;
using Calamari.Tests.Hooks;

namespace Calamari.Tests.Module
{
    public class TestModule : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ScriptHookMock>().As<IScriptWrapper>().AsSelf().SingleInstance();
        }
    }
}
