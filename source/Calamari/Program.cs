using System;
using System.Linq;
using Calamari.Commands.Support;

namespace Calamari
{
    class Program
    {
        static int Main(string[] args)
        {
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
