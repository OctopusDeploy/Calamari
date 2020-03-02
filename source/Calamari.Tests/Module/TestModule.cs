using Autofac;
using Calamari.Commands.Support;
using Calamari.Hooks;
using Calamari.Tests.Commands;
using Calamari.Tests.Hooks;
using NUnit.Framework.Interfaces;

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
