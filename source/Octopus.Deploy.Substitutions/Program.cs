using System;
using System.IO;
using System.Text;
using Octopus.Deploy.Startup;
using Octostache;

namespace Octopus.Deploy.Substitutions
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                if (args.Length != 3)
                {
                    Console.Error.WriteLine("Usage: Octopus.Deploy.Substitutions.exe <source> <variables> <target>");
                    return 1;
                }

                var sourceFile = EnsureExists(MapPath(args[0]));
                var variablesFile = EnsureExists(MapPath(args[1]));
                var targetFile = MapPath(args[2]);

                var variables = new VariableDictionary(variablesFile);
                var source = File.ReadAllText(sourceFile);

                var result = variables.Evaluate(source);
                File.WriteAllText(targetFile, result);

                return 0;
            }
            catch (Exception ex)
            {
                return ConsoleFormatter.PrintError(ex);
            }
        }

        static string MapPath(string path)
        {
            return Path.GetFullPath(path);
        }

        static string EnsureExists(string path)
        {
            if (!File.Exists(path))
            {
                throw new ArgumentException("Could not find file: " + path);
            }

            return path;
        }
    }
}
