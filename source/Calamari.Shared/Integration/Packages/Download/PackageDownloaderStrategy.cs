using System;
using System.Net;
using System.Net.Http;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.Download.Helm;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    /// <summary>
    /// This class knows how to interpret a package id and request a download
    /// from a specific downloader implementation. 
    /// </summary>
    public class PackageDownloaderStrategy
    {
        readonly IScriptEngine engine;
        readonly ICalamariFileSystem fileSystem;
        readonly IFreeSpaceChecker freeSpaceChecker;
        readonly ICommandLineRunner commandLineRunner;

        public PackageDownloaderStrategy(IScriptEngine engine, ICalamariFileSystem fileSystem, IFreeSpaceChecker freeSpaceChecker, ICommandLineRunner commandLineRunner)
        {
            this.engine = engine;
            this.fileSystem = fileSystem;
            this.freeSpaceChecker = freeSpaceChecker;
            this.commandLineRunner = commandLineRunner;
        }
        
        public PackagePhysicalFileMetadata DownloadPackage(
            string packageId,
            IVersion version,
            string feedId,
            Uri feedUri,
            FeedType feedType,
            ICredentials feedCredentials,
            bool forcePackageDownload,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            IPackageDownloader downloader = null;
            switch (feedType)
            {
                case FeedType.Maven:
                    downloader = new MavenPackageDownloader(fileSystem, freeSpaceChecker);
                    break;
                case FeedType.NuGet:
                    downloader = new NuGetPackageDownloader(fileSystem, freeSpaceChecker);
                    break;
                case FeedType.GitHub:
                    downloader = new GitHubPackageDownloader(fileSystem, freeSpaceChecker);
                    break;
                case FeedType.Helm:
                    downloader = new HelmChartPackageDownloader(fileSystem);
                    break;
                case FeedType.Docker:
                case FeedType.AwsElasticContainerRegistry :
                    downloader = new DockerImagePackageDownloader(engine, fileSystem, commandLineRunner);
                    break;
                default:
                    throw new NotImplementedException($"No Calamari downloader exists for feed type `{feedType}`.");
            }
            Log.Verbose($"Feed type provided `{feedType}` using {downloader.GetType().Name}");

            return downloader.DownloadPackage(
                packageId,
                version, 
                feedId, 
                feedUri, 
                feedCredentials, 
                forcePackageDownload, 
                maxDownloadAttempts, 
                downloadAttemptBackoff);
        }
    }
}