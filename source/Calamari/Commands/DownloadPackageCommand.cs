using System;
using System.Globalization;
using System.Net;
using Calamari.Commands.Support;
using Calamari.Integration.Packages.Download;
using Octopus.Versioning;
using Octopus.Versioning.Factories;
using Octopus.Versioning.Metadata;

namespace Calamari.Commands
{
    [Command("download-package", Description = "Downloads a NuGet package from a NuGet feed")]
    public class DownloadPackageCommand : Command
    {
        static readonly IPackageDownloader PackageDownloader = new PackageDownloaderStrategy();
        static readonly IVersionFactory VersionFactory = new VersionFactory();
        string packageId;
        string packageVersion;
        bool forcePackageDownload;
        string feedId;
        string feedUri;
        string feedUsername;
        string feedPassword;
        string maxDownloadAttempts = "5";
        string attemptBackoffSeconds = "10";
        
        public DownloadPackageCommand()
        {
            Options.Add("packageId=", "Package ID to download", v => packageId = v);
            Options.Add("packageVersion=", "Package version to download", v => packageVersion = v);
            Options.Add("feedId=", "Id of the NuGet feed", v => feedId = v);
            Options.Add("feedUri=", "URL to NuGet feed", v => feedUri = v);
            Options.Add("feedUsername=", "[Optional] Username to use for an authenticated NuGet feed", v => feedUsername = v);
            Options.Add("feedPassword=", "[Optional] Password to use for an authenticated NuGet feed", v => feedPassword = v);
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

                PackageDownloader.DownloadPackage(
                    packageId,
                    version,
                    feedId,
                    uri,
                    GetFeedCredentials(feedUsername, feedPassword),
                    forcePackageDownload,
                    parsedMaxDownloadAttempts,
                    parsedAttemptBackoff,
                    out string downloadedTo,
                    out string hash,
                    out long size);

                Log.VerboseFormat("Package {0} {1} successfully downloaded from feed: '{2}'", packageId, version,
                    feedUri);

                Log.SetOutputVariable("StagedPackage.Hash", hash);
                Log.SetOutputVariable("StagedPackage.Size", size.ToString(CultureInfo.InvariantCulture));
                Log.SetOutputVariable("StagedPackage.FullPathOnRemoteMachine", downloadedTo);
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("Failed to download package {0} {1} from feed: '{2}'", packageId, packageVersion, feedUri);
                return ConsoleFormatter.PrintError(ex);
            }

            return 0;
        }

        static ICredentials GetFeedCredentials(string feedUsername, string feedPassword)
        {
            ICredentials credentials = CredentialCache.DefaultNetworkCredentials;
            if (!String.IsNullOrWhiteSpace(feedUsername))
            {
                credentials = new NetworkCredential(feedUsername, feedPassword);
            }
            return credentials;
        }

        // ReSharper disable UnusedParameter.Local
        static void CheckArguments(
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

            var packageMetadata = new MetadataFactory().GetMetadataFromPackageID(packageId);
            if (!VersionFactory.TryCreateVersion(packageVersion, out version, packageMetadata.VersionFormat))
            {
                throw new CommandException($"Package version '{packageVersion}' specified is not a valid version string"); 
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
        // ReSharper restore UnusedParameter.Local
    }
}
