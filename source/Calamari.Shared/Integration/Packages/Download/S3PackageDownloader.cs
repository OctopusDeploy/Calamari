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
                throw new InvalidOperationException("Invalid PackageId for S3 feed. Expecting format `<bucketName>/<packageId>`");
            }

            var cacheDirectory = PackageDownloaderUtils.GetPackageRoot(feedId);

            for (var retry = 0; retry < maxDownloadAttempts; ++retry)
            {
                try
                {
                    using (var s3Client = new AmazonS3Client(new BasicAWSCredentials(feedUsername, feedPassword)))
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
#else
                        var response = s3Client.GetObjectAsync(bucketName, fileName).GetAwaiter().GetResult();
#endif
                        response.WriteResponseStreamToFile(localDownloadName);
                        var packagePhysicalFileMetadata = PackagePhysicalFileMetadata.Build(localDownloadName);
                        return packagePhysicalFileMetadata
                               ?? throw new CommandException($"Unable to retrieve metadata for package {packageId}, version {version}");
                    }
                }
                catch (Exception ex)
                {
                    if (retry == maxDownloadAttempts)
                        throw new CommandException($"Unable to download package {packageId} {version}: " + ex.Message);
                    Thread.Sleep(downloadAttemptBackoff);
                }
            }

            throw new CommandException($"Failed to download package {packageId} {version}");
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
