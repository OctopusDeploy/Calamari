using System;
using System.IO;
using System.Reflection;

namespace Calamari.Commands.Support
{
    public class ConsoleFormatter
    {
        public static int PrintError(Exception ex)
        {
            if (ex is CommandException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(ex.Message);
                Console.ResetColor();
                return 1;
            }
            if (ex is ReflectionTypeLoadException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(ex.ToString());
                Console.ResetColor();

                foreach (var loaderException in ((ReflectionTypeLoadException)ex).LoaderExceptions)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(loaderException.ToString());
                    Console.ResetColor();

                    if (!(loaderException is FileNotFoundException))
                        continue;

                    var exFileNotFound = loaderException as FileNotFoundException;
                    if (!string.IsNullOrEmpty(exFileNotFound.FusionLog))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine(exFileNotFound.FusionLog);
                        Console.ResetColor();
                    }
                }

                return 43;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(ex.Message);
            Console.ResetColor();
            Console.WriteLine(ex.ToString());
            return 100;
        }
    }
}
