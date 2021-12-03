using System;
using System.Net;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
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
        readonly IVariables variables;
        readonly ILog log;
        readonly IManagePackageUse packageJournal;

        public PackageDownloaderStrategy(
            ILog log,
            IScriptEngine engine,
            ICalamariFileSystem fileSystem,
            IFreeSpaceChecker freeSpaceChecker,
            ICommandLineRunner commandLineRunner,
            IVariables variables,
            IManagePackageUse packageJournal)
        {
            this.log = log;
            this.engine = engine;
            this.fileSystem = fileSystem;
            this.freeSpaceChecker = freeSpaceChecker;
            this.commandLineRunner = commandLineRunner;
            this.variables = variables;
            this.packageJournal = packageJournal;
        }

        public PackagePhysicalFileMetadata DownloadPackage(string packageId,
                                                           IVersion version,
                                                           string feedId,
                                                           Uri feedUri,
                                                           FeedType feedType,
                                                           string feedUsername,
                                                           string feedPassword,
                                                           bool forcePackageDownload,
                                                           int maxDownloadAttempts,
                                                           TimeSpan downloadAttemptBackoff)
        {
            IPackageDownloader? downloader = null;
            switch (feedType)
            {
                case FeedType.Maven:
                    downloader = new MavenPackageDownloader(fileSystem, freeSpaceChecker);
                    break;
                case FeedType.NuGet:
                    downloader = new NuGetPackageDownloader(fileSystem, freeSpaceChecker, variables, packageJournal);
                    break;
                case FeedType.GitHub:
                    downloader = new GitHubPackageDownloader(log, fileSystem, freeSpaceChecker);
                    break;
                case FeedType.Helm:
                    downloader = new HelmChartPackageDownloader(fileSystem);
                    break;
                case FeedType.Docker:
                case FeedType.AwsElasticContainerRegistry :
                    downloader = new DockerImagePackageDownloader(engine, fileSystem, commandLineRunner, variables);
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
                feedUsername,
                feedPassword,
                forcePackageDownload,
                maxDownloadAttempts,
                downloadAttemptBackoff);
        }
    }
}