using Autofac;
using Calamari.Commands.Support;
using Calamari.Integration.Proxies;
using Calamari.Modules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Calamari
{
    public class Program
    {
        readonly string displayName;
        readonly string informationalVersion;
        readonly string[] environmentInformation;
        private readonly IEnumerable<ICommand> commands;

        public Program(
            string displayName, 
            string informationalVersion, 
            string[] environmentInformation,
            IEnumerable<ICommand> commands)
        {
            this.displayName = displayName;
            this.informationalVersion = informationalVersion;
            this.environmentInformation = environmentInformation;
            this.commands = commands;
        }

        static int Main(string[] args)
        {
            using (var container = BuildContainer())
            {
                return container.Resolve<Program>().Execute(args);
            }
        }

        public static IContainer BuildContainer()
        {
            var builder = new ContainerBuilder();            
            builder.RegisterModule(new CalamariProgramModule());
            builder.RegisterModule(new CalamariCommandsModule());
            builder.RegisterModule(new CalamariPluginsModule());
            return builder.Build();
        }

        public int Execute(string[] args)
        {
            Log.Verbose($"Octopus Deploy: {displayName} version {informationalVersion}");
            Log.Verbose($"Environment Information:{Environment.NewLine}" +
                        $"  {string.Join($"{Environment.NewLine}  ", environmentInformation)}");

            ProxyInitializer.InitializeDefaultProxy();

            try
            {
                var action = GetFirstArgument(args);
                var command = LocateCommand(action);
                if (command == null)
                    return PrintHelp(action);
                return command.Execute(args.Skip(1).ToArray());
            }
            catch (Exception ex)
            {
                return ConsoleFormatter.PrintError(ex);
            }            
        }

        private static string GetFirstArgument(string[] args)
        {
            return (args.FirstOrDefault() ?? string.Empty).Trim('-', '/');
        }

        private ICommand LocateCommand(string action)
        {                                
            if (string.IsNullOrWhiteSpace(action))
                return null;

            return commands.FirstOrDefault(command => CommandHasName(command, action));
        }

        /// <summary>
        /// Check to see if a given command has the attribute matching the action
        /// </summary>
        /// <param name="command">The command to check</param>
        /// <param name="action">The name of the action</param>
        /// <returns>true if the command matches the action name, and false otherwise</returns>
        private bool CommandHasName(ICommand command, string action)
        {
            return command.GetType().GetCustomAttributes(typeof(CommandAttribute), true)
                .Select(attr => (ICommandMetadata) attr)
                .Any(attr => attr.Name == action || attr.Aliases.Any(a => a == action));
        }

        private static int PrintHelp(string action)
        {
            return new HelpCommand(false).Execute(new[] { action });
        }
    }
}
