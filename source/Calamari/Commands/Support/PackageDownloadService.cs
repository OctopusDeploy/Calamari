using System;
using System.Globalization;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Packages.Download;

namespace Calamari.Commands.Support
{
    public class PackageDownloadService
    {
        readonly IScriptEngine scriptEngine;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;
        readonly ILog log;

        public PackageDownloadService(
            IScriptEngine scriptEngine,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner,
            ILog log)
        {
            this.scriptEngine = scriptEngine;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.log = log;
        }

        public PackagePhysicalFileMetadata DownloadPackage(PackageDownloadOptions options)
        {
            variables.Set(AuthenticationVariables.FeedType, options.FeedType.ToString());

            PackageDownloadArgumentValidator.CheckArguments(
                options.PackageId,
                options.PackageVersion,
                options.FeedId,
                options.FeedUri,
                options.FeedUsername,
                options.FeedPassword,
                options.MaxDownloadAttempts,
                options.AttemptBackoffSeconds,
                options.FeedType,
                options.VersionFormat,
                variables,
                out var version,
                out var uri,
                out var parsedMaxDownloadAttempts,
                out var parsedAttemptBackoff);

            var packageDownloaderStrategy = new PackageDownloaderStrategy(
                log,
                scriptEngine,
                fileSystem,
                commandLineRunner,
                variables);

            var pkg = packageDownloaderStrategy.DownloadPackage(
                options.PackageId,
                version,
                options.FeedId,
                uri,
                options.FeedType,
                options.FeedUsername,
                options.FeedPassword,
                options.ForcePackageDownload,
                parsedMaxDownloadAttempts,
                parsedAttemptBackoff);

            log.VerboseFormat("Package {0} v{1} successfully downloaded from feed: '{2}'",
                options.PackageId, version, options.FeedUri);

            SetOutputVariables(pkg);

            return pkg;
        }

        void SetOutputVariables(PackagePhysicalFileMetadata pkg)
        {
            log.SetOutputVariableButDoNotAddToVariables("StagedPackage.Hash", pkg.Hash);
            log.SetOutputVariableButDoNotAddToVariables("StagedPackage.Size",
                pkg.Size.ToString(CultureInfo.InvariantCulture));
            log.SetOutputVariableButDoNotAddToVariables("StagedPackage.FullPathOnRemoteMachine",
                pkg.FullFilePath);
        }
    }
}
