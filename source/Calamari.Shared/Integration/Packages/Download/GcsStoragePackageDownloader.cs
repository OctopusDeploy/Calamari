using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Octopus.CoreUtilities.Extensions;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    public class GcsStoragePackageDownloader : IPackageDownloader
    {
        const string Extension = ".zip";
        const char BucketFileSeparator = '/';

        // first item will be used as the default extension before checking for others
        static readonly string[] KnownFileExtensions =
        {
            ".", // try to find a singular file without extension first
            ".zip", ".tar.gz", ".tar.bz2", ".tgz", ".tar.bz"
        };

        readonly IVariables variables;
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;

        static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();

        public GcsStoragePackageDownloader(IVariables variables, ILog log, ICalamariFileSystem fileSystem)
        {
            this.variables = variables;
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

        public PackagePhysicalFileMetadata DownloadPackage(
            string packageId,
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
                throw new InvalidOperationException($"Invalid PackageId for GCS feed. Expecting format `<bucketName>/<packageId>`, but received {bucketName}/{prefix}");
            }

            var cacheDirectory = PackageDownloaderUtils.GetPackageRoot(feedId);
            fileSystem.EnsureDirectoryExists(cacheDirectory);

            Log.VerboseFormat($"Checking package cache for package {packageId} v{version.ToString()}");
            var downloaded = GetFileFromCache(packageId, version, forcePackageDownload, cacheDirectory, KnownFileExtensions);
            if (downloaded != null)
            {
                return downloaded;
            }

            var retry = 0;
            for (; retry < maxDownloadAttempts; ++retry)
            {
                try
                {
                    log.Verbose($"Attempting download of package {packageId} version {version} from GCS bucket {bucketName}. Attempt #{retry + 1}");

                    var storageClient = CreateStorageClient(feedPassword);
                    string? foundFilePath = null;

                    for (int i = 0; i < KnownFileExtensions.Length && foundFilePath == null; i++)
                    {
                        var fileName = BuildFileName(prefix, version.ToString(), KnownFileExtensions[i]);
                        foundFilePath = FindSingleFileInBucket(storageClient, bucketName, fileName, CancellationToken.None)
                                        .GetAwaiter()
                                        .GetResult();
                    }

                    var fullFileName = !foundFilePath.IsNullOrEmpty()
                        ? foundFilePath
                        : throw new Exception($"Unable to download package {packageId} {version}: file not found");

                    var knownExtension = KnownFileExtensions.FirstOrDefault(extension => fullFileName!.EndsWith(extension))
                                         ?? Path.GetExtension(fullFileName)
                                         ?? Extension;

                    // Now we know the extension check for the package in the local cache
                    downloaded = GetFileFromCache(packageId, version, forcePackageDownload, cacheDirectory, new[] { knownExtension });
                    if (downloaded != null)
                    {
                        return downloaded;
                    }

                    var localDownloadName = Path.Combine(cacheDirectory, PackageName.ToCachedFileName(packageId, version, knownExtension));

                    using (var outputStream = File.Create(localDownloadName))
                    {
                        storageClient.DownloadObject(bucketName, fullFileName, outputStream);
                    }

                    var packagePhysicalFileMetadata = PackagePhysicalFileMetadata.Build(localDownloadName);
                    return packagePhysicalFileMetadata
                           ?? throw new CommandException($"Unable to retrieve metadata for package {packageId}, version {version}");
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

        static StorageClient CreateStorageClient(string? serviceAccountJsonKey)
        {
            if (string.IsNullOrEmpty(serviceAccountJsonKey))
            {
                // Use Application Default Credentials
                return StorageClient.Create();
            }

            // Use service account JSON key (feedPassword contains the JSON key)
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(serviceAccountJsonKey));
            var credential = GoogleCredential.FromStream(stream);
            return StorageClient.Create(credential);
        }

        PackagePhysicalFileMetadata? GetFileFromCache(
            string packageId,
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

        static async Task<string?> FindSingleFileInBucket(
            StorageClient client,
            string bucketName,
            string prefix,
            CancellationToken cancellationToken)
        {
            var objects = client.ListObjectsAsync(bucketName, prefix);
            var objectsList = new System.Collections.Generic.List<string>();

            await foreach (var obj in objects.WithCancellation(cancellationToken))
            {
                objectsList.Add(obj.Name);
                if (objectsList.Count > 1) break;
            }

            return objectsList.Count == 1 ? objectsList[0] : null;
        }
    }
}
