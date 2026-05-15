using System;
using System.Globalization;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Packages.Download;
using Octopus.Versioning;

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
        
        public PackagePhysicalFileMetadata DownloadPackageForRegistration(PackageDownloadAndRegisterOptions options)
        {
            var validatedPackageDownloadOptions = ValidateCommonOptions(options);
            Guard.NotNullOrWhiteSpace(options.TaskId, "No task ID was specified. Please pass --taskId YourTaskId");
            return DownloadPackageUsingStrategy(options, validatedPackageDownloadOptions);
        }

        public PackagePhysicalFileMetadata DownloadPackage(PackageDownloadOptions options)
        {
            var validatedPackageDownloadOptions = ValidateCommonOptions(options);
            return DownloadPackageUsingStrategy(options,validatedPackageDownloadOptions);
        }

        PackagePhysicalFileMetadata DownloadPackageUsingStrategy(IPackageDownloadOptions options, ValidatedPackageDownloadOptions validatedPackageDownloadOptions)
        {
            var packageDownloaderStrategy = new PackageDownloaderStrategy(
                                                                          log,
                                                                          scriptEngine,
                                                                          fileSystem,
                                                                          commandLineRunner,
                                                                          variables);

            var pkg = packageDownloaderStrategy.DownloadPackage(
                                                                options.PackageId,
                                                                validatedPackageDownloadOptions.Version,
                                                                options.FeedId,
                                                                validatedPackageDownloadOptions.Uri,
                                                                options.FeedType,
                                                                options.FeedUsername,
                                                                options.FeedPassword,
                                                                options.ForcePackageDownload,
                                                                validatedPackageDownloadOptions.ParsedMaxDownloadAttempts,
                                                                validatedPackageDownloadOptions.ParsedAttemptBackoff);

            log.VerboseFormat("Package {0} v{1} successfully downloaded from feed: '{2}'",
                              options.PackageId, validatedPackageDownloadOptions.Version, options.FeedUri);

            SetOutputVariables(pkg);
            
            return pkg;
        }
        
        public ValidatedPackageDownloadOptions ValidateCommonOptions(IPackageDownloadOptions options)
        {
            variables.Set(AuthenticationVariables.FeedType, options.FeedType.ToString());

            Guard.NotNullOrWhiteSpace(options.PackageId, "No package ID was specified. Please pass --packageId YourPackage");
            Guard.NotNullOrWhiteSpace(options.PackageVersion, "No package version was specified. Please pass --packageVersion 1.0.0.0");
            Guard.NotNullOrWhiteSpace(options.FeedId, "No feed ID was specified. Please pass --feedId feed-id");

            var usingOidc = !string.IsNullOrWhiteSpace(variables.Get("Jwt"));
            if (options.FeedType != FeedType.S3 && options.FeedType != FeedType.AwsElasticContainerRegistry)
            {
                Guard.NotNullOrWhiteSpace(options.FeedUri, "No feed URI was specified. Please pass --feedUri https://url/to/nuget/feed");
            }

            var version = VersionFactory.TryCreateVersion(options.PackageVersion, options.VersionFormat);
            if (version == null)
            {
                throw new CommandException($"Package version '{options.PackageVersion}' specified is not a valid {options.VersionFormat.ToString()} version string");
            }

            Uri? uri;
            if (options.FeedType == FeedType.S3 || options.FeedType == FeedType.AwsElasticContainerRegistry)
            {
                uri = null;
            }
            else if (!Uri.TryCreate(options.FeedUri, UriKind.Absolute, out uri))
                throw new CommandException($"URI specified '{options.FeedUri}' is not a valid URI");

            if (!String.IsNullOrWhiteSpace(options.FeedUsername) && String.IsNullOrWhiteSpace(options.FeedPassword) && !usingOidc)
                throw new CommandException("A username was specified but no password was provided. Please pass --feedPassword \"FeedPassword\"");

            if (!int.TryParse(options.MaxDownloadAttempts, out var parsedMaxDownloadAttempts))
                throw new CommandException($"The requested number of download attempts '{options.MaxDownloadAttempts}' is not a valid integer number");

            if (parsedMaxDownloadAttempts <= 0)
                throw new CommandException("The requested number of download attempts should be more than zero");

            if (!int.TryParse(options.AttemptBackoffSeconds, out var parsedAttemptBackoffSeconds))
                throw new CommandException($"Retry requested download attempt retry backoff '{options.AttemptBackoffSeconds}' is not a valid integer number of seconds");

            if (parsedAttemptBackoffSeconds < 0)
                throw new CommandException("The requested download attempt retry backoff should be a positive integer number of seconds");
            
            return new ValidatedPackageDownloadOptions(version, uri, parsedMaxDownloadAttempts, TimeSpan.FromSeconds(parsedAttemptBackoffSeconds));
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

    public class ValidatedPackageDownloadOptions
    {
        public ValidatedPackageDownloadOptions(IVersion version, Uri uri, int parsedMaxDownloadAttempts, TimeSpan parsedAttemptBackoff)
        {
            Version = version;
            Uri = uri;
            ParsedMaxDownloadAttempts = parsedMaxDownloadAttempts;
            ParsedAttemptBackoff = parsedAttemptBackoff;
        }

        public IVersion Version { get; }
        public Uri Uri { get; } 
        public int ParsedMaxDownloadAttempts { get; }
        public TimeSpan ParsedAttemptBackoff { get; }
    }
}
