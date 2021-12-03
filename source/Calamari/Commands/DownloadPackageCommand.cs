using System;
using System.Globalization;
using System.Net;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention;
using Calamari.Integration.Packages.Download;
using Octopus.Versioning;

namespace Calamari.Commands
{
    [Command("download-package", Description = "Downloads a NuGet package from a NuGet feed")]
    [PackageLockingCommand]
    public class DownloadPackageCommand : Command
    {
        private readonly IScriptEngine scriptEngine;
        readonly IFreeSpaceChecker freeSpaceChecker;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly ICommandLineRunner commandLineRunner;
        readonly IManagePackageUse packageJournal;

        string packageId;
        string packageVersion;
        bool forcePackageDownload;
        string feedId;
        string feedUri;
        string feedUsername;
        string feedPassword;
        string maxDownloadAttempts = "5";
        string attemptBackoffSeconds = "10";
        private FeedType feedType = FeedType.NuGet;
        private VersionFormat versionFormat = VersionFormat.Semver;

        public DownloadPackageCommand(
            IScriptEngine scriptEngine,
            IFreeSpaceChecker freeSpaceChecker,
            IVariables variables,
            ICalamariFileSystem fileSystem,
			ICommandLineRunner commandLineRunner,
            ILog log,
            IManagePackageUse packageJournal)
        {
            this.scriptEngine = scriptEngine;
            this.freeSpaceChecker = freeSpaceChecker;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.log = log;
            this.packageJournal = packageJournal;
            this.commandLineRunner = commandLineRunner;
            Options.Add("packageId=", "Package ID to download", v => packageId = v);
            Options.Add("packageVersion=", "Package version to download", v => packageVersion = v);
            Options.Add("packageVersionFormat=", $"[Optional] Format of version. Options {string.Join(", ", Enum.GetNames(typeof(VersionFormat)))}. Defaults to `{VersionFormat.Semver}`.",
                v =>
                {
                    if (!Enum.TryParse(v, out VersionFormat format))
                    {
                        throw new CommandException($"The provided version format `{format}` is not recognised.");
                    }
                    versionFormat = format;
                });
            Options.Add("feedId=", "Id of the feed", v => feedId = v);
            Options.Add("feedUri=", "URL to feed", v => feedUri = v);
            Options.Add("feedUsername=", "[Optional] Username to use for an authenticated feed", v => feedUsername = v);
            Options.Add("feedPassword=", "[Optional] Password to use for an authenticated feed", v => feedPassword = v);
            Options.Add("feedType=", $"[Optional] Type of feed. Options {string.Join(", ", Enum.GetNames(typeof(FeedType)))}. Defaults to `{FeedType.NuGet}`.",
                v =>
                {
                    if (!Enum.TryParse(v, out FeedType type))
                    {
                        throw new CommandException($"The provided feed type `{type}` is not recognised.");
                    }

                    feedType = type;
                });
            Options.Add("attempts=", $"[Optional] The number of times to attempt downloading the package. Default: {maxDownloadAttempts}", v => maxDownloadAttempts = v);
            Options.Add("attemptBackoffSeconds=", $"[Optional] The number of seconds to apply as a linear backoff between each download attempt. Default: {attemptBackoffSeconds}", v => attemptBackoffSeconds = v);
            Options.Add("forcePackageDownload", "[Optional, Flag] if specified, the package will be downloaded even if it is already in the package cache", v => forcePackageDownload = true);
        }


        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            try
            {
                CheckArguments(
                    packageId,
                    packageVersion,
                    feedId,
                    feedUri,
                    feedUsername,
                    feedPassword,
                    maxDownloadAttempts,
                    attemptBackoffSeconds,
                    out var version,
                    out var uri,
                    out var parsedMaxDownloadAttempts,
                    out var parsedAttemptBackoff);

                var packageDownloaderStrategy = new PackageDownloaderStrategy(log,
                    scriptEngine,
                    fileSystem,
                    freeSpaceChecker,
                    commandLineRunner,
                    variables,
                    packageJournal);
                var pkg = packageDownloaderStrategy.DownloadPackage(
                    packageId,
                    version,
                    feedId,
                    uri,
                    feedType,
                    feedUsername,
                    feedPassword,
                    forcePackageDownload,
                    parsedMaxDownloadAttempts,
                    parsedAttemptBackoff);

                log.VerboseFormat("Package {0} v{1} successfully downloaded from feed: '{2}'", packageId, version, feedUri);
                log.SetOutputVariableButDoNotAddToVariables("StagedPackage.Hash", pkg.Hash);
                log.SetOutputVariableButDoNotAddToVariables("StagedPackage.Size", pkg.Size.ToString(CultureInfo.InvariantCulture));
                log.SetOutputVariableButDoNotAddToVariables("StagedPackage.FullPathOnRemoteMachine", pkg.FullFilePath);
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Failed to download package {0} v{1} from feed: '{2}'", packageId, packageVersion, feedUri);
                return ConsoleFormatter.PrintError(log, ex);
            }

            return 0;
        }

        // ReSharper disable UnusedParameter.Local
        void CheckArguments(
            string packageId,
            string packageVersion,
            string feedId,
            string feedUri,
            string feedUsername,
            string feedPassword,
            string maxDownloadAttempts,
            string attemptBackoffSeconds,
            out IVersion version,
            out Uri uri,
            out int parsedMaxDownloadAttempts,
            out TimeSpan parsedAttemptBackoff)
        {
            Guard.NotNullOrWhiteSpace(packageId, "No package ID was specified. Please pass --packageId YourPackage");
            Guard.NotNullOrWhiteSpace(packageVersion, "No package version was specified. Please pass --packageVersion 1.0.0.0");
            Guard.NotNullOrWhiteSpace(feedId, "No feed ID was specified. Please pass --feedId feed-id");
            Guard.NotNullOrWhiteSpace(feedUri, "No feed URI was specified. Please pass --feedUri https://url/to/nuget/feed");

            version = VersionFactory.TryCreateVersion(packageVersion, versionFormat);
            if (version == null)
            {
                throw new CommandException($"Package version '{packageVersion}' specified is not a valid {versionFormat.ToString()} version string");
            }

            if (!Uri.TryCreate(feedUri, UriKind.Absolute, out uri))
                throw new CommandException($"URI specified '{feedUri}' is not a valid URI");

            if (!String.IsNullOrWhiteSpace(feedUsername) && String.IsNullOrWhiteSpace(feedPassword))
                throw new CommandException("A username was specified but no password was provided. Please pass --feedPassword \"FeedPassword\"");

            if (!int.TryParse(maxDownloadAttempts, out parsedMaxDownloadAttempts))
                throw new CommandException($"The requested number of download attempts '{maxDownloadAttempts}' is not a valid integer number");

            if (parsedMaxDownloadAttempts <= 0)
                throw new CommandException("The requested number of download attempts should be more than zero");

            int parsedAttemptBackoffSeconds;
            if (!int.TryParse(attemptBackoffSeconds, out parsedAttemptBackoffSeconds))
                throw new CommandException($"Retry requested download attempt retry backoff '{attemptBackoffSeconds}' is not a valid integer number of seconds");

            if (parsedAttemptBackoffSeconds < 0)
                throw new CommandException("The requested download attempt retry backoff should be a positive integer number of seconds");

            parsedAttemptBackoff = TimeSpan.FromSeconds(parsedAttemptBackoffSeconds);
        }
    }
}