using System;
using System.Diagnostics;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.Proxies;
using Calamari.Util.Environments;
using System.Reflection;
using Calamari.Util;

namespace Calamari
{
    public class Program
    {
        readonly string displayName;
        readonly string informationalVersion;
        readonly string[] environmentInformation;

        public Program(string displayName, string informationalVersion, string[] environmentInformation)
        {
            this.displayName = displayName;
            this.informationalVersion = informationalVersion;
            this.environmentInformation = environmentInformation;
        }

        static int Main(string[] args)
        {
            var program = new Program("Calamari", typeof(Program).Assembly.GetInformationalVersion(), EnvironmentHelper.SafelyGetEnvironmentInformation());
            return program.Execute(args);
        }

        public int Execute(string[] args)
        {
            Log.Verbose($"Octopus Deploy: {displayName} version {informationalVersion}");
            Log.Verbose($"Environment Information:{Environment.NewLine}" +
                $"  {string.Join($"{Environment.NewLine}  ", environmentInformation)}");

            ProxyInitializer.InitializeDefaultProxy();
            RegisterCommandAssemblies();

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

        protected virtual void RegisterCommandAssemblies()
        {
            CommandLocator.Instance.RegisterAssemblies(typeof(Program).Assembly);
        }

        private static string GetFirstArgument(string[] args)
        {
            return (args.FirstOrDefault() ?? string.Empty).Trim('-', '/');
        }

        private static ICommand LocateCommand(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return null;

            var locator = CommandLocator.Instance;
            return locator.Find(action);
        }

        private static int PrintHelp(string action)
        {
            return new HelpCommand(false).Execute(new[] { action });
        }
    }
}
