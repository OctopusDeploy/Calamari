using Autofac;
using Calamari.Commands.Support;
using Calamari.Integration.Proxies;
using Calamari.Modules;
using Calamari.Util;
using System;
using System.Linq;
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

        public Program(
            string displayName, 
            string informationalVersion, 
            string[] environmentInformation,
            ICommand command)
        {
            this.displayName = displayName;
            this.informationalVersion = informationalVersion;
            this.environmentInformation = environmentInformation;
            this.command = command;
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
            var builder = new ContainerBuilder();            
            builder.RegisterModule(new CalamariProgramModule());
            builder.RegisterModule(new CalamariCommandsModule(PluginUtils.GetFirstArgument(args), typeof(Program).Assembly));
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

        private static int PrintHelp(string action)
        {
            return new HelpCommand(false).Execute(new[] { action });
        }
    }
}
