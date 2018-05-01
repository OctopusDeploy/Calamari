using System;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Builder;
using Calamari.Commands;
using Calamari.Commands.Support;
using Octopus.CoreUtilities.Extensions;
using Module = Autofac.Module;

namespace Calamari.Modules
{
    /// <summary>
    /// Autofac module to register the calamari commands.
    /// Note that commands were never designed to all be instantiated. We can
    /// only register the commands that are actually being used, rather than
    /// registering them all and filtering later.
    /// </summary>
    public class CalamariHelpCommandsModule : Module
    {
        private static readonly CommandLocator CommandLocator = new CommandLocator();
        private readonly string commandName;
        private readonly string helpCommandName;
        private readonly Assembly assembly;

        public CalamariHelpCommandsModule(string commandName, string helpCommandName, Assembly assembly)
        {
            this.commandName = commandName;
            this.helpCommandName = helpCommandName;
            this.assembly = assembly;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // Only in the event that the primary command was the help command do
            // we go ahead and register the Command object that the help command
            // requested.
            if (CommandLocator.Find(commandName, ThisAssembly) == typeof(HelpCommand))
            {
                RegisterHelpCommand(builder);
            }
        }

        /// <summary>
        /// Register the command that the HelpCommand displays help for. This is registered
        /// as an unnamed ICommand that is then consumed by the HelpCommand constructor.
        /// </summary>
        private Type RegisterHelpCommand(ContainerBuilder builder) =>
            CommandLocator.Find(helpCommandName, assembly)?
                .Tee(helpCommand => builder.RegisterType(helpCommand).As<ICommand>().SingleInstance());
    }
}
