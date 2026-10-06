using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace Calamari.AzureWebApp.NetCoreShim
{
    public class Program
    {
        [Verb("sync", HelpText = "Synchronizes a local directory with a remote destination")]
        public class SyncOptions
        {
            [Option("sourcePath", Required = true)]
            public string SourceContentPath { get; set; }

            [Option("destPath", Required = true)]
            public string DestinationContentPath { get; set; }

            [Option("destUserName", Required = true)]
            public string DestinationUserName { get; set; }

            [Option("destPassword", Required = true)]
            public string DestinationPassword { get; set; }

            [Option("destUri", Required = true)]
            public Uri DestinationUri { get; set; }

            [Option("destSite", Required = true)]
            public string DestinationDeploymentSite { get; set; }

            [Option("useChecksum", Default = false)]
            public bool UseChecksum { get; set; }

            [Option("doNotDelete", Default = false)]
            public bool DoNotDelete { get; set; }

            [Option("useAppOffline", Default = false)]
            public bool UseAppOffline { get; set; }

            [Option("preserveAppData", Default = false)]
            public bool DoPreserveAppData { get; set; }

            [Option("preservePaths", Separator = '|')]
            public IEnumerable<string> PreservePaths { get; set; }

            [Option("encryptionKey", Required = true)]
            public string EncryptionKey { get; set; }
        }

        public static async Task<int> Main(string[] args)
        {
            var logger = new LoggerConfiguration()
                         .MinimumLevel.Verbose()
                         // Errors go to stderr so that Calamari can report them as the reason the deployment failed
                         .WriteTo.Console(outputTemplate: "{Level:u3}|{Message:lj}{NewLine}",
                                          theme: ConsoleTheme.None,
                                          standardErrorFromLevel: LogEventLevel.Error)
                         .CreateLogger();

            return await Parser.Default.ParseArguments<SyncOptions>(args)
                               .MapResult(async o =>
                                          {
                                              var executor = new WebDeploymentExecutor(logger);
                                              try
                                              {
                                                  await executor.Execute(o);
                                                  return 0;
                                              }
                                              catch (Exception e)
                                              {
                                                  LogException(logger, e);
                                                  return 1;
                                              }
                                          },
                                          err => Task.FromResult(1));
        }

        // Calamari reads our output one line at a time and only understands lines that start with a level prefix,
        // so the exception is written line by line rather than through the output template's {Exception} token.
        // Web Deploy puts the real cause in the inner exceptions, so each one's message is logged as an error.
        static void LogException(ILogger logger, Exception exception)
        {
            string previous = null;
            for (var ex = exception; ex != null; ex = ex.InnerException)
            {
                var firstLine = SplitLines(ex.Message).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstLine) && firstLine != previous)
                {
                    logger.Error("{Line:l}", firstLine);
                }

                previous = firstLine;
            }

            foreach (var line in SplitLines(exception.ToString()))
            {
                logger.Verbose("{Line:l}", line);
            }
        }

        static IEnumerable<string> SplitLines(string text)
        {
            return (text ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}