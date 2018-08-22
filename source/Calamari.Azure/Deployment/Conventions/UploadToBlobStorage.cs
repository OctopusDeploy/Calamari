using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.Azure.Accounts;
using Calamari.Azure.Integration;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Azure.Deployment.Conventions
{
    public class UploadToBlobStorage : IInstallConvention
    {
        private readonly UploadToBlobStorageOptions options;
        private readonly ICalamariFileSystem fileSystem;
        private readonly IFileSubstituter substituter;
        private readonly AzureServicePrincipalAccount account;

        public UploadToBlobStorage(UploadToBlobStorageOptions options, ICalamariFileSystem fileSystem,
            IFileSubstituter substituter, AzureServicePrincipalAccount account)
        {
            this.options = options;
            this.fileSystem = fileSystem;
            this.substituter = substituter;
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
            var resourceGroupName = deployment.Variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);
            var storageEndpointSuffix = deployment.Variables.Get(SpecialVariables.Action.Azure.StorageEndPointSuffix,
                DefaultVariables.StorageEndpointSuffix);

            var storageAccountPrimaryKey =
                await GetStorageAccountPrimaryKey(account, storageAccountName, resourceGroupName).ConfigureAwait(false);
            var cloudStorage =
                new CloudStorageAccount(new StorageCredentials(storageAccountName, storageAccountPrimaryKey),
                    storageEndpointSuffix, true);

            if (options.UploadPackage)
            {
                await Upload(cloudStorage, Path.GetDirectoryName(deployment.PackageFilePath),
                    new[] {deployment.PackageFilePath}).ConfigureAwait(false);
                return;
            }

            var files = new HashSet<string>();

            foreach (var path in options.FilePaths)
            {
                var filePath = Path.Combine(deployment.StagingDirectory, path);

                if (!fileSystem.FileExists(filePath))
                {
                    throw new FileNotFoundException($"The file '{path}' could not be found in the package.");
                }

                files.Add(filePath);
            }

            files.UnionWith(fileSystem.EnumerateFilesWithGlob(deployment.StagingDirectory, options.Globs.ToArray()));
            if (!files.Any())
            {
                Log.Info(
                    $"The glob patterns {options.Globs.ToSingleQuotedCommaSeperated()} didn't match any files. Nothing was uploaded to Azure Blob Storage.");
                return;
            }

            var substitutionPatterns = options.SubstitutionPatterns;

            new SubstituteInFilesConvention(fileSystem, substituter,
                    _ => substitutionPatterns.Any(),
                    _ => substitutionPatterns)
                .Install(deployment);

            await Upload(cloudStorage, deployment.StagingDirectory, files).ConfigureAwait(false);
        }

        private async Task Upload(CloudStorageAccount cloudStorage, string baseDir, IEnumerable<string> files)
        {
            var blobClient = cloudStorage.CreateCloudBlobClient();
            blobClient.DefaultRequestOptions.StoreBlobContentMD5 = true;
            var container = blobClient.GetContainerReference(options.ContainerName);

            if (!container.Exists())
            {
                throw new Exception(
                    $"Storage Container named '{options.ContainerName}' does not exist. Make sure the container exists.");
            }

            foreach (var file in files)
            {
                var blobName = ConvertToRelativeUri(file, baseDir);
                var blob = container.GetBlockBlobReference(blobName);

                if (await blob.ExistsAsync().ConfigureAwait(false)) // I don't think does a MD5 check :(
                {
                    Log.VerboseFormat(
                        "A blob named {0} already exists with the same length, so it will be used instead of uploading the new package.",
                        blob.Name);
                    continue;
                }

                await UploadBlobInChunks(new FileInfo(file), blob, blobClient).ConfigureAwait(false);
            }

            Log.Info("Package upload complete");
        }

        private static async Task UploadBlobInChunks(FileInfo fileInfo, CloudBlockBlob blob, CloudBlobClient blobClient)
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

            await blobClient.SetServicePropertiesAsync(blobClient.GetServiceProperties(), null, operationContext)
                .ConfigureAwait(false);

            Log.VerboseFormat("Uploading the package to blob storage. The package file is {0}.",
                fileInfo.Length.ToFileSizeString());

            using (var fileReader = fileInfo.OpenRead())
            {
                var blockList = new List<string>();

                long uploadedSoFar = 0;

                var data = new byte[1024 * 1024];
                var id = 1;

                while (true)
                {
                    id++;

                    var read = await fileReader.ReadAsync(data, 0, data.Length).ConfigureAwait(false);
                    if (read == 0)
                    {
                        await blob.PutBlockListAsync(blockList).ConfigureAwait(false);
                        break;
                    }

                    var blockId =
                        Convert.ToBase64String(
                            Encoding.UTF8.GetBytes(id.ToString(CultureInfo.InvariantCulture).PadLeft(30, '0')));
                    await blob.PutBlockAsync(blockId, new MemoryStream(data, 0, read, true), null)
                        .ConfigureAwait(false);
                    blockList.Add(blockId);

                    uploadedSoFar += read;

                    Log.ServiceMessages.Progress((int) (uploadedSoFar * 100 / fileInfo.Length),
                        $"Uploading {fileInfo.Name} to blob storage");
                }
            }

            Log.Verbose("Upload complete");
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