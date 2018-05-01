using Autofac;
using Calamari.Commands.Support;
using Calamari.Integration.Proxies;
using Calamari.Modules;
using Calamari.Util;
using System;
using System.Linq;
using Calamari.Commands;
using Calamari.Extensions;

namespace Calamari
{
    public class Program
    {
        private static readonly IPluginUtils PluginUtils = new PluginUtils();
        readonly string displayName;
        readonly string informationalVersion;
        readonly string[] environmentInformation;
        private readonly ICommand command;
        private readonly HelpCommand helpCommand;

        public Program(
            string displayName, 
            string informationalVersion, 
            string[] environmentInformation,
            ICommand command,
            HelpCommand helpCommand)
        {
            this.displayName = displayName;
            this.informationalVersion = informationalVersion;
            this.environmentInformation = environmentInformation;
            this.command = command;
            this.helpCommand = helpCommand;
        }

        static int Main(string[] args)
        {
            using (var container = BuildContainer(args))
            {
                return container.Resolve<Program>().Execute(args);
            }
        }

        public static IContainer BuildContainer(string[] args)
        {
            var firstArg = PluginUtils.GetFirstArgument(args);
            var secondArg = PluginUtils.GetSecondArgument(args);

            var builder = new ContainerBuilder();            
            builder.RegisterModule(new CalamariProgramModule());
            builder.RegisterModule(new CalamariCommandsModule(
                firstArg,
                secondArg,
                typeof(CalamariCommandsModule).Assembly));
            builder.RegisterModule(new CalamariCommandsModule(
                firstArg,
                secondArg,
                typeof(Program).Assembly));
            builder.RegisterModule(new CommonModule(args));

            foreach (var module in new ModuleLoader(args).AllModules)
            {
                builder.RegisterModule(module);
            }

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
                if (command == null)
                {
                    return PrintHelp(PluginUtils.GetFirstArgument(args));
                }

                return command.Execute(args.Skip(1).ToArray());
            }
            catch (Exception ex)
            {
                return ConsoleFormatter.PrintError(ex);
            }            
        }

        private int PrintHelp(string action)
        {
            helpCommand.HelpWasAskedFor = false;
            return helpCommand.Execute(new[] { action });
        }
    }
}
