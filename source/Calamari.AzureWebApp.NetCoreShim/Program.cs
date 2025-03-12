using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLine;
using Serilog;
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
                         .WriteTo.Console(outputTemplate: "{Level:u3}|{Message:lj}{NewLine}", theme: ConsoleTheme.None)
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
                                                  logger.Error(e, e.Message);
                                                  return 1;
                                              }
                                          },
                                          err => Task.FromResult(1));
        }
    }
}