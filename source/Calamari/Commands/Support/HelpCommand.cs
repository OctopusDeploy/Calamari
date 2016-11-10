using System;
using System.IO;
using System.Linq;
using Calamari.Util;
using System.Reflection;

namespace Calamari.Commands.Support
{
    [Command("help", "?", "h", Description = "Prints this help text")]
    public class HelpCommand : ICommand
    {
        private readonly bool helpWasAskedFor;
        readonly ICommandLocator commands;

        public HelpCommand() : this(true)
        {
        }

        public HelpCommand(bool helpWasAskedFor) : this(CommandLocator.Instance)
        {
            this.helpWasAskedFor = helpWasAskedFor;
        }

        public HelpCommand(ICommandLocator commands)
        {
            this.commands = commands;
        }

        public void GetHelp(TextWriter writer)
        {
        }

        public int Execute(string[] commandLineArguments)
        {
            var executable = Path.GetFileNameWithoutExtension(typeof (HelpCommand).GetTypeInfo().Assembly.Location);

            var commandName = commandLineArguments.FirstOrDefault();

            if (string.IsNullOrEmpty(commandName))
            {
                PrintGeneralHelp(executable);
            }
            else
            {
                var command = commands.Find(commandName);

                if (command == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Log.StdOut.WriteLine("Command '{0}' is not supported", commandName);
                    Console.ResetColor();
                    PrintGeneralHelp(executable);
                }
                else
                {
                    PrintCommandHelp(executable, command, commandName);
                }
            }

            return helpWasAskedFor ? 0 : 1;
        }

        void PrintCommandHelp(string executable, ICommand command, string commandName)
        {
            Console.ResetColor();
            Log.StdOut.Write("Usage: ");
            Console.ForegroundColor = ConsoleColor.White;
            Log.StdOut.WriteLine(executable + " " + commandName + " [<options>]");
            Console.ResetColor();
            Log.StdOut.WriteLine();
            Log.StdOut.WriteLine("Where [<options>] is any of: ");
            Log.StdOut.WriteLine();

            command.GetHelp(Log.StdOut);

            Log.StdOut.WriteLine();
        }

        void PrintGeneralHelp(string executable)
        {
            Console.ResetColor();
            Log.StdOut.Write("Usage: ");
            Console.ForegroundColor = ConsoleColor.White;
            Log.StdOut.WriteLine(executable + " <command> [<options>]");
            Console.ResetColor();
            Log.StdOut.WriteLine();
            Log.StdOut.WriteLine("Where <command> is one of: ");
            Log.StdOut.WriteLine();

            foreach (var possible in commands.List().OrderBy(x => x.Name))
            {
                Console.ForegroundColor = ConsoleColor.White;
                Log.StdOut.WriteLine("  " + possible.Name.PadRight(15, ' '));
                Console.ResetColor();
                Log.StdOut.WriteLine("   " + possible.Description);
            }

            Log.StdOut.WriteLine();
            Log.StdOut.Write("Or use ");
            Console.ForegroundColor = ConsoleColor.White;
            Log.StdOut.Write(executable + " help <command>");
            Console.ResetColor();
            Log.StdOut.WriteLine(" for more details.");
        }
    }
}