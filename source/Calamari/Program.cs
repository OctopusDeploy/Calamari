using System;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.Proxies;

namespace Calamari
{
    public class Program
    {
        readonly string displayName;
        readonly string informationalVersion;

        public Program(string displayName, string informationalVersion)
        {
            this.displayName = displayName;
            this.informationalVersion = informationalVersion;
        }

        static int Main(string[] args)
        {
            var program = new Program("Calamari", typeof(Program).Assembly.GetInformationalVersion());
            return program.Execute(args);
        }

        protected int Execute(string[] args)
        {
            Log.Verbose($"Octopus Deploy: {displayName} version {informationalVersion}");

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
