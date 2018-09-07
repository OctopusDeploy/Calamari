using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Azure.Accounts;
using Calamari.Azure.Deployment.Integration.BlobStorage;
using Calamari.Azure.Integration;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Util;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.Core.Util;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Azure.Deployment.Conventions
{
    public class UploadToBlobStorage : IInstallConvention
    {
        private static readonly bool IsMD5Supported = HashCalculator.IsAvailableHashingAlgorithm(MD5.Create);
        private static readonly BlobRequestOptions BlobRequestOptionsDefault = new BlobRequestOptions {StoreBlobContentMD5 = IsMD5Supported, DisableContentMD5Validation = false, UseTransactionalMD5 = false};

        private readonly UploadToBlobStorageOptions options;
        private readonly ICalamariFileSystem fileSystem;
        private readonly AzureServicePrincipalAccount account;

        public UploadToBlobStorage(UploadToBlobStorageOptions options, ICalamariFileSystem fileSystem,
            AzureServicePrincipalAccount account)
        {
            this.options = options;
            this.fileSystem = fileSystem;
            this.account = account;
        }

        public void Install(RunningDeployment deployment)
        {
            InstallAsync(deployment).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public static async Task<string> GetStorageAccountPrimaryKey(AzureServicePrincipalAccount account,
            string storageAccountName, string resourceGroupName)
        {
            using (var storageManagementClient = await account.CreateStorageManagementClient().ConfigureAwait(false))
            {
                var getKeysResponse = await storageManagementClient.StorageAccounts
                    .ListKeysWithHttpMessagesAsync(resourceGroupName, storageAccountName).ConfigureAwait(false);

                return getKeysResponse.Body.Keys.First(key => key.KeyName == "key1").Value;
            }
        }

        private async Task InstallAsync(RunningDeployment deployment)
        {
            var storageAccountName = deployment.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName);
            var resourceGroupName = deployment.Variables.Get(AzureSpecialVariables.BlobStorage.ResourceGroupName);
            var storageEndpointSuffix = deployment.Variables.Get(SpecialVariables.Action.Azure.StorageEndPointSuffix,
                DefaultVariables.StorageEndpointSuffix);

            var storageAccountPrimaryKey =
                await GetStorageAccountPrimaryKey(account, storageAccountName, resourceGroupName).ConfigureAwait(false);
            var cloudStorage =
                new CloudStorageAccount(new StorageCredentials(storageAccountName, storageAccountPrimaryKey),
                    storageEndpointSuffix, true);

            if (options.UploadEntirePackage)
            {
                options.FilePaths.Add(new FileSelectionProperties {Pattern = "**/*"});
            }

            var files = new HashSet<string>();
            var patterns = new List<string>();
            var metadata = new Dictionary<string, Dictionary<string, string>>();

            foreach (var properties in options.FilePaths)
            {
                var matched = fileSystem.EnumerateFilesWithGlob(deployment.StagingDirectory, properties.Pattern)
                    .ToArray();
                if (properties.FailIfNoMatches)
                {
                    if (!matched.Any())
                    {
                        throw new FileNotFoundException(
                            $"The glob patterns '{properties.Pattern}' didn't match any files.");
                    }
                }

                patterns.Add(properties.Pattern);

                files.UnionWith(matched);

                if (properties.Metadata.Count == 0)
                {
                    continue;
                }

                foreach (var matchFile in matched)
                {
                    if (metadata.TryGetValue(matchFile, out var dic))
                    {
                        foreach (var meta in properties.Metadata)
                        {
                            dic[meta.Key] = meta.Value;
                        }

                        continue;
                    }

                    metadata.Add(matchFile, new Dictionary<string, string>(properties.Metadata));
                }
            }

            if (!files.Any())
            {
                Log.Info(
                    $"The glob patterns {patterns.ToSingleQuotedCommaSeperated()} didn't match any files. Nothing was uploaded to Azure Blob Storage.");
                return;
            }

            await Upload(cloudStorage, deployment.StagingDirectory, files, metadata).ConfigureAwait(false);
        }

        private async Task Upload(CloudStorageAccount cloudStorage, string baseDir, IEnumerable<string> files,
            IReadOnlyDictionary<string, Dictionary<string, string>> metadata)
        {
            var blobClient = cloudStorage.CreateCloudBlobClient();
            blobClient.DefaultRequestOptions.StoreBlobContentMD5 = true;
            var container = blobClient.GetContainerReference(options.ContainerName);

            if (!container.Exists())
            {
                throw new Exception(
                    $"Storage Container named '{options.ContainerName}' does not exist. Make sure the container exists.");
            }

            MD5 md5 = null;

            if (IsMD5Supported)
            {
                md5 = MD5.Create();
            }

            foreach (var file in files)
            {
                var blobName = ConvertToRelativeUri(file, baseDir);
                var blob = container.GetBlockBlobReference(blobName);
                var uploadBlob = true;

                if (await blob.ExistsAsync().ConfigureAwait(false)) // I don't think does a MD5 check :(
                {
                    Log.VerboseFormat("A blob named {0} already exists.", blob.Name);
                    
                    if (IsMD5Supported)
                    {
                        Log.VerboseFormat("Checking md5 {0} to see if we need to upload or not.", blob.Name);
                        var md5Hash = Convert.ToBase64String(HashCalculator.Hash(file, () => md5));
                        if (md5Hash == blob.Properties.ContentMD5)
                        {
                            Log.VerboseFormat("Blob {0} content matches md5, no need to upload.", blob.Name);
                            uploadBlob = false;
                        }
                    }
                }

                if (uploadBlob)
                {
                    await UploadBlobWithProgress(new FileInfo(file), blob).ConfigureAwait(false);
                }

                if (!metadata.TryGetValue(file, out var dic))
                {
                    continue;
                }

                foreach (var meta in dic)
                {
                    blob.Metadata[meta.Key] = meta.Value;
                }

                await blob.SetMetadataAsync().ConfigureAwait(false);
            }

            Log.Info("Package upload complete");
        }

        private static async Task UploadBlobWithProgress(FileInfo fileInfo, CloudBlockBlob blob)
        {
            var operationContext = new OperationContext();
            operationContext.ResponseReceived += delegate(object sender, RequestEventArgs args)
            {
                var statusCode = (int) args.Response.StatusCode;
                var statusDescription = args.Response.ReasonPhrase;
                if (statusCode >= 400)
                {
                    Log.Error(
                        $"Error when uploading the package. Azure returned a HTTP status code of: {statusCode} ({statusDescription})");
                    Log.Verbose("The upload will be retried");

                    return;
                }

                Log.Verbose($"Uploading, response received: {statusCode} ({statusDescription})");
            };

            Log.VerboseFormat("Uploading the package to blob storage. The package file is {0}.",
                fileInfo.Length.ToFileSizeString());
            
            await blob.UploadFromFileAsync(fileInfo.FullName, null, BlobRequestOptionsDefault, null,
                new Progress<StorageProgress>(progress =>
                {
                    var percentage = (int) (progress.BytesTransferred * 100 / fileInfo.Length);

                    Log.ServiceMessages.Progress(percentage, $"Uploading {fileInfo.Name} to blob storage");
                }), CancellationToken.None).ConfigureAwait(false);

            Log.Verbose($"Upload {fileInfo.Name} complete");
        }

        private static string ConvertToRelativeUri(string filePath, string baseDir)
        {
            var uri = new Uri(filePath);
            if (!baseDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                baseDir += Path.DirectorySeparatorChar.ToString();
            }

            var baseUri = new Uri(baseDir);
            return baseUri.MakeRelativeUri(uri).ToString();
        }
    }
}