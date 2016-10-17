using System;
using System.Globalization;
using System.Net;
using Calamari.Commands.Support;
#if USE_NUGET_V2_LIBS
using Calamari.NuGet.Versioning;
#else
using NuGet.Versioning;
#endif
using PackageDownloader = Calamari.Integration.Packages.Download.PackageDownloader;

namespace Calamari.Commands
{
    [Command("download-package", Description = "Downloads a NuGet package from a NuGet feed")]
    public class DownloadPackageCommand : Command
    {
        readonly static PackageDownloader PackageDownloader = new PackageDownloader();
        string packageId;
        string packageVersion;
        bool forcePackageDownload;
        string feedId;
        string feedUri;
        string feedUsername;
        string feedPassword;
        
        public DownloadPackageCommand()
        {
            Options.Add("packageId=", "Package ID to download", v => packageId = v);
            Options.Add("packageVersion=", "Package version to download", v => packageVersion = v);
            Options.Add("feedId=", "Id of the NuGet feed", v => feedId = v);
            Options.Add("feedUri=", "URL to NuGet feed", v => feedUri = v);
            Options.Add("feedUsername=", "[Optional] Username to use for an authenticated NuGet feed", v => feedUsername = v);
            Options.Add("feedPassword=", "[Optional] Password to use for an authenticated NuGet feed", v => feedPassword = v);
            Options.Add("forcePackageDownload", "[Optional, Flag] if specified, the package will be downloaded even if it is already in the package cache", v => forcePackageDownload = true);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            try
            {
                NuGetVersion version;
                Uri uri;
                CheckArguments(packageId, packageVersion, feedId, feedUri, feedUsername, feedPassword, out version, out uri);

                string downloadedTo;
                string hash;
                long size;
                PackageDownloader.DownloadPackage(
                    packageId,
                    version,
                    feedId,
                    uri,
                    GetFeedCredentials(feedUsername, feedPassword),
                    forcePackageDownload,
                    out downloadedTo,
                    out hash,
                    out size);

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
        static void CheckArguments(string packageId, string packageVersion, string feedId, string feedUri, string feedUsername, string feedPassword, out NuGetVersion version, out Uri uri)
        {
            Guard.NotNullOrWhiteSpace(packageId, "No package ID was specified. Please pass --packageId YourPackage");
            Guard.NotNullOrWhiteSpace(packageVersion, "No package version was specified. Please pass --packageVersion 1.0.0.0");
            Guard.NotNullOrWhiteSpace(feedId, "No feed ID was specified. Please pass --feedId feed-id");
            Guard.NotNullOrWhiteSpace(feedUri, "No feed URI was specified. Please pass --feedUri https://url/to/nuget/feed");

            if (!NuGetVersion.TryParse(packageVersion, out version))
                throw new CommandException(String.Format("Package version '{0}' specified is not a valid semantic version", packageVersion));

            if (!Uri.TryCreate(feedUri, UriKind.Absolute, out uri))
                throw new CommandException(String.Format("URI specified '{0}' is not a valid URI", feedUri));

            if (!String.IsNullOrWhiteSpace(feedUsername) && String.IsNullOrWhiteSpace(feedPassword))
                throw new CommandException("A username was specified but no password was provided. Please pass --feedPassword \"FeedPassword\"");
        }
        // ReSharper restore UnusedParameter.Local
    }
}
