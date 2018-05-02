using System;
using Autofac;
using Autofac.Core;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Integration.Scripting;
using Calamari.Util.Environments;

namespace Calamari.Modules
{
    class CalamariProgramModule : Module
    {
        private static readonly ICommandLocator CommandLocator = new CommandLocator();
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<Program>()
                .WithParameter("displayName", "Calamari")
                .WithParameter("informationalVersion", typeof(Program).Assembly.GetInformationalVersion())
                .WithParameter("environmentInformation", EnvironmentHelper.SafelyGetEnvironmentInformation())
                .WithParameter(
                    new ResolvedParameter(
                        (pi, ctx) => pi.ParameterType == typeof(ICommand),
                        (pi, ctx) => CommandLocator.GetOptionalNamedCommand(ctx, CalamariCommandsModule.RunCommand)))
                .SingleInstance();
            builder.RegisterType<CombinedScriptEngine>().AsSelf();
            builder.RegisterType<HelpCommand>().AsSelf();
        }
    }
}