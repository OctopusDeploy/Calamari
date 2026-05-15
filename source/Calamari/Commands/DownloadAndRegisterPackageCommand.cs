using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Octopus.Versioning;

namespace Calamari.Commands
{
    [Command("download-and-register-package",
        Description = "Downloads a package and atomically registers its use in the package journal")]
    public class DownloadAndRegisterPackageCommand : Command
    {
        readonly ILog log;
        readonly IManagePackageCache journal;
        readonly PackageDownloadService downloadService;
        readonly PackageDownloadAndRegisterOptions options;

        public DownloadAndRegisterPackageCommand(
            IScriptEngine scriptEngine,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner,
            ILog log,
            IManagePackageCache journal)
        {
            this.log = log;
            this.journal = journal;
            this.downloadService = new PackageDownloadService(scriptEngine, variables, fileSystem, commandLineRunner, log);
            this.options = new PackageDownloadAndRegisterOptions();
            PackageDownloadAndRegisterOptions.ConfigureOptions(Options, options);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            try
            {
                var pkg = downloadService.DownloadPackageForRegistration(options);
                var version = VersionFactory.TryCreateVersion(options.PackageVersion, options.VersionFormat);
                RegisterPackageUse(pkg, version);
                return 0;
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Failed to download and register package {0} v{1} from feed: '{2}'",
                    options.PackageId, options.PackageVersion, options.FeedUri);
                return ConsoleFormatter.PrintError(log, ex);
            }
        }

        void RegisterPackageUse(PackagePhysicalFileMetadata pkg, IVersion version)
        {
            var package = new PackageIdentity(
                new PackageId(pkg.PackageId),
                version,
                new PackagePath(pkg.FullFilePath));
            var size = (ulong)pkg.Size;
            journal.RegisterPackageUse(package, new ServerTaskId(options.TaskId), size);
        }

    }
}
