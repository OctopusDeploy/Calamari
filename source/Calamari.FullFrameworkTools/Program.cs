using System;
using System.Diagnostics;
using Calamari.FullFrameworkTools.Command;

namespace Calamari.FullFrameworkTools
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var log = new Log();
            if (ExtractArgs(args, out var cmd, out var password, out var file))
            {
                WriteHelp();
                return -1;
            }

            var commandLocator = new CommandLocator();
            var requestInvoker = new CommandRequestInvoker(commandLocator);

            if (cmd == "version")
            {
                var fileVersionInfo = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location);
                log.Info(fileVersionInfo.ProductVersion);
            }

            try
            {
                var result = requestInvoker.Run(cmd, password, file);
                log.Result(result);
            }
            catch (Exception ex)
            {
                log.Fatal(ex);
            }
            return 0;
        }

        static void WriteHelp()
        {
            Console.Error.WriteLine("Commands: iis, win-cert-store, version");
            Console.Error.WriteLine("Usage: <command> --password <password> --file <filepath>");
        }

        static bool ExtractArgs(string[] args,
                                out string cmd,
                                out string password,
                                out string file)
        {
            file = "";
            password = "";
            cmd = "";

            if (args.Length == 0)
            {
                return false;
            }
            cmd = args[0];

            if (args[1] == "--password")
            {
                password = args[2];
            }  else if (args[3] == "--password")
            {
                password = args[4];
            }
            
            if (args[1] == "--file")
            {
                file = args[2];
            }  else if (args[3] == "--file")
            {
                file = args[4];
            }

            return true;
        }
    }
}