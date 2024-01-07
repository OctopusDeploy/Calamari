using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Octopus.CoreUtilities.Extensions;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    public class S3PackageDownloader : IPackageDownloader
    {
        const string Extension = ".zip";

        // first item will be used as the default extension before checking for others
        static string[] knownFileExtensions =
        {
            ".", // try to find a singular file without extension first
            ".zip", ".tar.gz", ".tar.bz2", ".tar.gz", ".tgz", ".tar.bz"
        };

        const char BucketFileSeparator = '/';
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;

        static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();

        public S3PackageDownloader(ILog log, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.fileSystem = fileSystem;
        }

        string BuildFileName(string prefix, string version, string extension)
        {
            return $"{prefix}.{version}{extension}";
        }

        (string BucketName, string Filename) GetBucketAndKey(string searchTerm)
        {
            var splitString = searchTerm.Split(new[] { BucketFileSeparator }, 2);
            if (splitString.Length == 0)
                return ("", "");
            if (splitString.Length == 1)
                return (splitString[0], "");

            return (splitString[0], splitString[1]);
        }

        public PackagePhysicalFileMetadata DownloadPackage(string packageId,
                                                           IVersion version,
                                                           string feedId,
                                                           Uri feedUri,
                                                           string? feedUsername,
                                                           string? feedPassword,
                                                           bool forcePackageDownload,
                                                           int maxDownloadAttempts,
                                                           TimeSpan downloadAttemptBackoff)
        {
            var (bucketName, prefix) = GetBucketAndKey(packageId);
            if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(prefix))
            {
                throw new InvalidOperationException($"Invalid PackageId for S3 feed. Expecting format `<bucketName>/<packageId>`, but received ${bucketName}/{prefix}");
            }

            var cacheDirectory = PackageDownloaderUtils.GetPackageRoot(feedId);
            fileSystem.EnsureDirectoryExists(cacheDirectory);

            Log.VerboseFormat($"Checking package cache for package {packageId} v{version.ToString()}");
            var downloaded = GetFileFromCache(packageId, version, forcePackageDownload, cacheDirectory, knownFileExtensions);
            if (downloaded != null)
            {
                return downloaded;
            }

            int retry = 0;
            for (; retry < maxDownloadAttempts; ++retry)
            {
                try
                {
                    log.Verbose($"Attempting download of package {packageId} version {version} from S3 bucket {bucketName}. Attempt #{retry + 1}");

                    var region = GetBucketsRegion(feedUsername, feedPassword, bucketName);

                    using (var s3Client = GetS3Client(feedUsername, feedPassword, region))
                    {
                        string? foundFilePath = null;
                        for (int i = 0; i < knownFileExtensions.Length && foundFilePath == null; i++)
                        {
                            var fileName = BuildFileName(prefix, version.ToString(), knownFileExtensions[i]);
                            foundFilePath = FindSingleFileInTheBucket(s3Client, bucketName, fileName, CancellationToken.None)
                                            .GetAwaiter()
                                            .GetResult();
                        }

                        var fullFileName = !foundFilePath.IsNullOrEmpty() ? foundFilePath : throw new Exception($"Unable to download package {packageId} {version}: file not found");

                        var knownExtension = knownFileExtensions.FirstOrDefault(extension => fullFileName.EndsWith(extension))
                                             ?? Path.GetExtension(fullFileName)
                                             ?? Extension;

                        // Now we know the extension check for the package in the local cache
                        downloaded = GetFileFromCache(packageId, version, forcePackageDownload, cacheDirectory, new string[] { knownExtension });
                        if (downloaded != null)
                        {
                            return downloaded;
                        }

                        var localDownloadName = Path.Combine(cacheDirectory, PackageName.ToCachedFileName(packageId, version, knownExtension));
                        
                        var response = s3Client.GetObjectAsync(bucketName, fullFileName).GetAwaiter().GetResult();
                        response.WriteResponseStreamToFileAsync(localDownloadName, false, CancellationToken.None).GetAwaiter().GetResult();

                        var packagePhysicalFileMetadata = PackagePhysicalFileMetadata.Build(localDownloadName);
                        return packagePhysicalFileMetadata
                               ?? throw new CommandException($"Unable to retrieve metadata for package {packageId}, version {version}");
                    }
                }
                catch (Exception ex)
                {
                    log.Verbose($"Download attempt #{retry + 1} failed, with error: {ex.Message}. Retrying in {downloadAttemptBackoff}");

                    if ((retry + 1) == maxDownloadAttempts)
                        throw new CommandException($"Unable to download package {packageId} {version}: " + ex.Message);
                    Thread.Sleep(downloadAttemptBackoff);
                }
            }

            throw new CommandException($"Failed to download package {packageId} {version}. Attempted {retry} times.");
        }

        static AmazonS3Client GetS3Client(string? feedUsername, string? feedPassword, string endpoint = "us-west-1")
        {
            var config = new AmazonS3Config
            {
                AllowAutoRedirect = true,
                RegionEndpoint = RegionEndpoint.GetBySystemName(endpoint)
            };

            return string.IsNullOrEmpty(feedUsername) ? new AmazonS3Client(config) : new AmazonS3Client(new BasicAWSCredentials(feedUsername, feedPassword), config);
        }

        PackagePhysicalFileMetadata? GetFileFromCache(string packageId,
                                                         IVersion version,
                                                         bool forcePackageDownload,
                                                         string cacheDirectory,
                                                         string[] fileExtensions)
        {
            if (forcePackageDownload)
                return null;
            var downloaded = SourceFromCache(packageId, version, cacheDirectory, fileExtensions);
            if (downloaded == null)
                return null;
            Log.VerboseFormat("Package was found in cache. No need to download. Using file: '{0}'", downloaded.FullFilePath);
            return downloaded;
        }

        string GetBucketsRegion(string? feedUsername, string? feedPassword, string bucketName)
        {
            using (var s3Client = GetS3Client(feedUsername, feedPassword))
            {
                var region = s3Client.GetBucketLocationAsync(bucketName, CancellationToken.None).GetAwaiter().GetResult();

                string regionString = region.Location.Value;
                // If the bucket is in the us-east-1 region, then the region name is not included in the response.
                if (string.IsNullOrEmpty(regionString))
                {
                    regionString = "us-east-1";
                }
                else if (regionString.Equals("EU", StringComparison.OrdinalIgnoreCase))
                {
                    regionString = "eu-west-1";
                }

                return regionString;
            }
        }

        PackagePhysicalFileMetadata? SourceFromCache(string packageId, IVersion version, string cacheDirectory, string[] knownExtensions)
        {
            var files = fileSystem.EnumerateFilesRecursively(cacheDirectory, PackageName.ToSearchPatterns(packageId, version, knownExtensions));

            foreach (var file in files)
            {
                var package = PackageName.FromFile(file);
                if (package == null)
                    continue;

                var idMatches = string.Equals(package.PackageId, packageId, StringComparison.OrdinalIgnoreCase);
                var versionExactMatch = string.Equals(package.Version.ToString(), version.ToString(), StringComparison.OrdinalIgnoreCase);
                var semverMatches = package.Version.Equals(version);

                if (idMatches && (semverMatches || versionExactMatch))
                    return PackagePhysicalFileMetadata.Build(file, package);
            }

            return null;
        }

        async Task<string?> FindSingleFileInTheBucket(AmazonS3Client client, string bucketName, string prefix, CancellationToken cancellationToken)
        {
            var request = new ListObjectsRequest
            {
                BucketName = bucketName,
                Prefix = prefix
            };
            
            var response = await client.ListObjectsAsync(request, cancellationToken);
            return response.S3Objects.Count == 1 ? response.S3Objects[0].Key : null;
        }
    }
}