using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Calamari.Commands.Support
{
    [Command("help", "?", "h", Description = "Prints this help text")]
    public class HelpCommand : ICommand
    {
        private readonly IEnumerable<ICommandMetadata> commandMetadata;
        private readonly ICommand commandToHelpWith;
        public bool HelpWasAskedFor { get; set; } = true;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="commandMetadata"></param>
        /// <param name="commandToHelpWith">
        /// The command whose help text is to be displayed.
        /// This field has to be called commandToHelpWith, because this is expected by autofac.
        /// </param>
        public HelpCommand(IEnumerable<ICommandMetadata> commandMetadata, ICommand commandToHelpWith)
        {
            this.commandMetadata = commandMetadata;
            this.commandToHelpWith = commandToHelpWith;
        }

        public HelpCommand(IEnumerable<ICommandMetadata> commandMetadata)
        {
            this.commandMetadata = commandMetadata;
            this.commandToHelpWith = null;
        }

        public void GetHelp(TextWriter writer)
        {
        }

        public int Execute(string[] commandLineArguments)
        {
            var executable = Path.GetFileNameWithoutExtension(typeof (HelpCommand).Assembly.Location);

            var commandName = commandLineArguments.FirstOrDefault();

            if (string.IsNullOrEmpty(commandName))
            {
                PrintGeneralHelp(executable);
            }
            else
            {
                if (commandToHelpWith == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Command '{0}' is not supported", commandName);
                    Console.ResetColor();
                    PrintGeneralHelp(executable);
                }
                else
                {
                    PrintCommandHelp(executable, commandToHelpWith, commandName);
                }
            }

            return HelpWasAskedFor ? 0 : 1;
        }

        void PrintCommandHelp(string executable, ICommand command, string commandName)
        {
            Console.ResetColor();
            Console.Write("Usage: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(executable + " " + commandName + " [<options>]");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Where [<options>] is any of: ");
            Console.WriteLine();

            command.GetHelp(Console.Out);

            Console.WriteLine();
        }

        void PrintGeneralHelp(string executable)
        {
            Console.ResetColor();
            Console.Write("Usage: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(executable + " <command> [<options>]");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Where <command> is one of: ");
            Console.WriteLine();

            foreach (var possible in commandMetadata.OrderBy(x => x.Name))
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("  " + possible.Name.PadRight(15, ' '));
                Console.ResetColor();
                Console.WriteLine("   " + possible.Description);
            }

            Console.WriteLine();
            Console.Write("Or use ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(executable + " help <command>");
            Console.ResetColor();
            Console.WriteLine(" for more details.");
        }
    }
}