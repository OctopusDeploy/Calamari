using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Octopus.Versioning;

namespace Calamari.Commands
{
    [Command("download-package", Description = "Downloads a NuGet package from a NuGet feed")]
    public class DownloadPackageCommand : Command
    {
        readonly ILog log;
        readonly PackageDownloadService downloadService;
        readonly PackageDownloadOptions options;

        public DownloadPackageCommand(
            IScriptEngine scriptEngine,
            IVariables variables,
            ICalamariFileSystem fileSystem,
			ICommandLineRunner commandLineRunner,
            ILog log)
        {
            this.log = log;
            this.downloadService = new PackageDownloadService(scriptEngine, variables, fileSystem, commandLineRunner, log);
            this.options = new PackageDownloadOptions();

            PackageDownloadOptions.ConfigureOptions(Options, options);
        }


        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            try
            {
                downloadService.DownloadPackage(options);
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Failed to download package {0} v{1} from feed: '{2}'",
                    options.PackageId, options.PackageVersion, options.FeedUri);
                return ConsoleFormatter.PrintError(log, ex);
            }

            return 0;
        }

    }
}