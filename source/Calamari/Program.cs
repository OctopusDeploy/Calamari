using Autofac;
using Calamari.Commands.Support;
using Calamari.Integration.Proxies;
using Calamari.Modules;
using Calamari.Util;
using System;
using System.Linq;
using System.Net;
using System.Reflection;
using Calamari.Commands;
using Calamari.Extensions;
using Calamari.Util.Environments;

namespace Calamari
{
    public class Program
    {
        private static readonly IPluginUtils PluginUtils = new PluginUtils();
        /// <summary>
        /// Common assemblies that will always be present
        /// </summary>
        private static readonly Assembly[] KnownAssemblies = new[]
        {
            typeof(Program).Assembly,
            typeof(CalamariCommandsModule).Assembly
        };
        private readonly ICommand command;
        private readonly HelpCommand helpCommand;

        static int Main(string[] args)
        {
            EnableTLS12();
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

            // Register the program entry point
            builder.RegisterModule(new CalamariProgramModule());
            // This will register common utilities and services
            builder.RegisterModule(new CommonModule(args));
            // For all the common locations (i.e. this assembly and the shared one)
            // load any commands, and any commands to support the help command (if
            // required).
            foreach (var knownAssembly in KnownAssemblies)
            {
                builder.RegisterModule(new CalamariCommandsModule(firstArg, secondArg, knownAssembly));
            }
            // For the external libraries, let them load any additional
            // services via their module classes.
            foreach (var module in new ModuleLoader(args).AllModules)
            {
                builder.RegisterModule(module);
            }

            return builder.Build();
        }

        public Program(ICommand command, HelpCommand helpCommand)
        {
            this.command = command;
            this.helpCommand = helpCommand;
        }        

        public int Execute(string[] args)
        {
            Log.Verbose($"Octopus Deploy: Calamari version {typeof(Program).Assembly.GetInformationalVersion()}");
            Log.Verbose($"Environment Information:{Environment.NewLine}" +
                        $"  {string.Join($"{Environment.NewLine}  ", EnvironmentHelper.SafelyGetEnvironmentInformation())}");

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

        public static void EnableTLS12()
        {
            // https://developer.github.com/changes/2018-02-01-weak-crypto-removal-notice/
            // TLS1.2 was required to access GitHub apis as of 22 Feb 2018. 

            //https://central.sonatype.org/articles/2018/May/04/discontinue-support-for-tlsv11-and-below/
            // TLS1.1 and below was discontinued on MavenCentral as of 18 June 2018

            // Unfortunately SecurityProtocolType.TLS12 doesn't exist in net40 but its enum value works!
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
        }
    }
}
