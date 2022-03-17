using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        static string[] knownFileExtensions = { ".zip", ".tar.gz", ".tar.bz2", ".tar.gz", ".tgz", ".tar.bz" };
        
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
            var splitString = searchTerm.Split(new [] { BucketFileSeparator }, 2);
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
            
            if (!forcePackageDownload)
            {
                var downloaded = SourceFromCache(packageId, version, cacheDirectory);
                if (downloaded != null)
                {
                    Log.VerboseFormat("Package was found in cache. No need to download. Using file: '{0}'", downloaded.FullFilePath);
                    return downloaded;
                }
            }

            int retry = 0;
            for (; retry < maxDownloadAttempts; ++retry)
            {
                try
                {
                    log.Verbose($"Attempting download of package {packageId} version {version} from S3 bucket {bucketName}. Attempt #{retry + 1}");
                    
                    using (var s3Client = string.IsNullOrEmpty(feedUsername) ? new AmazonS3Client() : new AmazonS3Client(new BasicAWSCredentials(feedUsername, feedPassword)))
                    {
                        bool fileExists = false;
                        string fileName = "";
                        for (int i = 0; i < knownFileExtensions.Length && !fileExists; i++)
                        {
                            fileName = BuildFileName(prefix, version.ToString(), knownFileExtensions[i]);
                            fileExists = FileExistsInBucket(s3Client, bucketName, fileName, CancellationToken.None)
#if NET40
                                .Result;
#else
                                         .GetAwaiter()
                                         .GetResult();
#endif
                        }

                        if (!fileExists)
                            throw new Exception($"Unable to download package {packageId} {version}: file not found");

                        var localDownloadName = Path.Combine(cacheDirectory, PackageName.ToCachedFileName(packageId, version, "." + Path.GetExtension(fileName)));

#if NET40
                        var response = s3Client.GetObject(bucketName, fileName);
                        response.WriteResponseStreamToFile(localDownloadName);
#else
                        var response = s3Client.GetObjectAsync(bucketName, fileName).GetAwaiter().GetResult();
                        response.WriteResponseStreamToFileAsync(localDownloadName, false, CancellationToken.None).GetAwaiter().GetResult();
#endif
                        var packagePhysicalFileMetadata = PackagePhysicalFileMetadata.Build(localDownloadName);
                        return packagePhysicalFileMetadata
                               ?? throw new CommandException($"Unable to retrieve metadata for package {packageId}, version {version}");
                    }
                }
                catch (Exception ex)
                {
                    log.Verbose($"Download attempt #{retry + 1} failed, with error: {ex.Message}. Retrying in {downloadAttemptBackoff}");
                    
                    if (retry == maxDownloadAttempts)
                        throw new CommandException($"Unable to download package {packageId} {version}: " + ex.Message);
                    Thread.Sleep(downloadAttemptBackoff);
                }
            }

            throw new CommandException($"Failed to download package {packageId} {version}. Attempted {retry} times.");
        }
        
        PackagePhysicalFileMetadata? SourceFromCache(string packageId, IVersion version, string cacheDirectory)
        {
            Log.VerboseFormat($"Checking package cache for package {packageId} v{version.ToString()}");

            var files = fileSystem.EnumerateFilesRecursively(cacheDirectory, PackageName.ToSearchPatterns(packageId, version, knownFileExtensions));

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

        async Task<bool> FileExistsInBucket(AmazonS3Client client, string bucketName, string prefix, CancellationToken cancellationToken)
        {
            var request = new ListObjectsRequest
            {
                BucketName = bucketName,
                Prefix = prefix
            };

#if NET40
            var response = client.ListObjects(request);
#else
            var response = await client.ListObjectsAsync(request, cancellationToken);
#endif
            return response.S3Objects.Any();
        }
    }
}
