using Autofac;
using Calamari.Commands.Support;
using Octopus.CoreUtilities.Extensions;
using System;
using System.Reflection;
using Autofac.Core;
using Module = Autofac.Module;

namespace Calamari.Modules
{
    /// <summary>
    /// Autofac module to register the calamari commands.
    /// Note that commands were never designed to all be instantiated. We can
    /// only register the commands that are actually being used, rather than
    /// registering them all and filtering later.
    /// </summary>
    public class CalamariCommandsModule : Module
    {
        public static string RunCommand = "RunCommand";
        public static string HelpCommand = "HelpCommand";
        private static readonly ICommandLocator CommandLocator = new CommandLocator();
        private readonly string commandName;
        private readonly string helpCommandName;
        private readonly Assembly assembly;

        public CalamariCommandsModule(string commandName, string helpCommandName, Assembly assembly)
        {
            this.commandName = commandName;
            this.helpCommandName = helpCommandName;
            this.assembly = assembly;
        }

        protected override void Load(ContainerBuilder builder)
        {
            RegisterNormalCommand(builder);
            // Only in the event that the primary command was the help command do
            // we go ahead and register the Command object that the help command
            // will display the details of.
            if (CommandLocator.Find(commandName, ThisAssembly) == typeof(HelpCommand))
            {
                RegisterHelpCommand(builder);
            }
            RegisterCommandAttributes(builder);
        }

        /// <summary>
        /// Register a "normal" (i.e. not help) command as an ICommand. This is a named
        /// service that is used in CalamariProgramModule.
        /// </summary>
        private Type RegisterNormalCommand(ContainerBuilder builder) =>
            CommandLocator.Find(commandName, assembly)?
                .Tee(command => AddICommandToContext(builder, command, RunCommand));

        /// <summary>
        /// Register the command that the HelpCommand displays help for. This is registered
        /// as an unnamed ICommand that is then consumed by the HelpCommand constructor.
        /// </summary>
        private Type RegisterHelpCommand(ContainerBuilder builder) =>
            CommandLocator.Find(helpCommandName, assembly)?
                .Tee(helpCommand => AddICommandToContext(builder, helpCommand, HelpCommand));

        /// <summary>
        /// Register an ICommand with the builder with a name
        /// </summary>
        /// <param name="builder">The builder</param>
        /// <param name="command">The command type</param>
        /// <param name="name">The name to register the command as</param>
        private void AddICommandToContext(ContainerBuilder builder, Type command, string name) =>
            builder
                .RegisterType(command)
                .Named<ICommand>(name)
                .WithParameter(
                    new ResolvedParameter(
                        (pi, ctx) => pi.ParameterType == typeof(ICommand) && pi.Name == "commandToHelpWith",
                        (pi, ctx) => CommandLocator.GetOptionalNamedCommand(ctx, HelpCommand)))
                .SingleInstance();

        private void RegisterCommandAttributes(ContainerBuilder builder)
        {
            foreach (var commandMetadata in CommandLocator.List(assembly))
            {
                builder.RegisterInstance(commandMetadata).As<ICommandMetadata>();
            }
        }
    }
}
