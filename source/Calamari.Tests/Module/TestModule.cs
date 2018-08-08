using Autofac;
using Calamari.Commands.Support;
using Calamari.Hooks;
using Calamari.Shared;
using Calamari.Shared.Scripting;
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
            // It must be possible to register ICommand objects without affecting the way
            // the main command to be run and the help command are generated and injected.
            builder.RegisterType<RunTestScript>().As<ICommand>().AsSelf().SingleInstance();
        }
    }
}
