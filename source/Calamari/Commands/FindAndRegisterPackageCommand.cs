using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.FileSystem;
using Octopus.Versioning;

namespace Calamari.Commands
{
    [Command("find-and-register-package",
        Description = "Finds a package and atomically registers its use in the package journal")]
    public class FindAndRegisterPackageCommand : Command
    {
        readonly ILog log;
        readonly IManagePackageCache journal;
        readonly ICalamariFileSystem fileSystem;
        readonly PackageFindService findService;
        readonly PackageFindRegistrationOptions options;

        public FindAndRegisterPackageCommand(
            ILog log,
            IPackageStore packageStore,
            IManagePackageCache journal,
            ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.journal = journal;
            this.fileSystem = fileSystem;
            this.findService = new PackageFindService(log, packageStore);
            this.options = new PackageFindRegistrationOptions();

            PackageFindRegistrationOptions.ConfigureOptions(Options, options);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            try
            {
                var package = findService.FindPackageForRegistration(options);

                if (package != null)
                {
                    var version = VersionFactory.TryCreateVersion(options.PackageVersion, options.VersionFormat);
                    RegisterPackageUse(package, version);
                }

                return 0;
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Failed to find and register package {0} v{1} hash {2}",
                    options.PackageId, options.PackageVersion, options.PackageHash);
                return ConsoleFormatter.PrintError(log, ex);
            }
        }

        void RegisterPackageUse(PackagePhysicalFileMetadata pkg, IVersion version)
        {
            // A package without a path cannot be registered. This check was previously done in Octopus Server
            // but has been moved here so that registration is fully self-contained within Calamari.
            if (string.IsNullOrEmpty(pkg.FullFilePath))
                return;

            var package = new PackageIdentity(
                new PackageId(pkg.PackageId),
                version,
                new PackagePath(pkg.FullFilePath));
            var size = fileSystem.GetFileSize(package.Path.Value);
            journal.RegisterPackageUse(package, new ServerTaskId(options.TaskId), (ulong)size);
        }
    }
}
