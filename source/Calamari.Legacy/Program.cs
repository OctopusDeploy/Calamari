using System;
using System.Linq;
using Calamari.Legacy.Iis;

namespace Calamari.Legacy
{
    public static class Log
    {
        public static void Verbose(string text)
        {
            
        }
    }

    internal class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Commands: iis, win-cert-store, version");
                return 1;
            }

            var cmd = args[0];
            switch (cmd)
            {
                case "iis":
                    var subcmd = args[1];
                    var cmd2 = new IisLegacyCommand(new InternetInformationServer());
                    if (subcmd == cmd2.Name)
                    {
                        cmd2.Execute(args.Skip(2).ToArray());
                    }
                    break;
                case "win-cert-store":
                    break;
                case "version":
                    Console.WriteLine($"Calamari Version: {typeof(Program).Assembly.GetName().Version}");
                    break;
                default:
                    Console.Error.WriteLine("Unknown Command");
                    return 1;
            }

            return 0;
        }
    }
}