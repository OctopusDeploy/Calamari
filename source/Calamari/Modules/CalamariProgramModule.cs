using System;
using Autofac;
using Autofac.Core;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Journal;
using Calamari.Hooks;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Scripting;

namespace Calamari.Modules
{
    class CalamariProgramModule : Module
    {
        private static readonly ICommandLocator CommandLocator = new CommandLocator();
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<Program>()
                .WithParameter(
                    new ResolvedParameter(
                        (pi, ctx) => pi.ParameterType == typeof(ICommand),
                        (pi, ctx) => CommandLocator.GetOptionalNamedCommand(ctx, CalamariCommandsModule.RunCommand)))
                .SingleInstance();
            builder.RegisterType<DeploymentJournalWriter>().As<IDeploymentJournalWriter>();
            builder.Register((_) => CalamariPhysicalFileSystem.GetPhysicalFileSystem()).As<ICalamariFileSystem>();
            builder.RegisterType<CombinedScriptEngine>().AsSelf();
            
            builder
                .RegisterAssemblyTypes(this.GetType().Assembly)
                .AssignableTo<IScriptWrapper>()
                .As<IScriptWrapper>()
                .SingleInstance();
        }
    }
}