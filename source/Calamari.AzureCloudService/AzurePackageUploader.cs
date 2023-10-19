using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Util;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Management.Storage;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Calamari.AzureCloudService
{
    public class AzurePackageUploader
    {
        readonly ILog log;
        const string OctopusPackagesContainerName = "octopuspackages";

        public AzurePackageUploader(ILog log)
        {
            this.log = log;
        }

        public async Task<Uri> Upload(SubscriptionCloudCredentials credentials, string storageAccountName, string packageFile, string uploadedFileName, string storageEndpointSuffix, string serviceManagementEndpoint)
        {
            var cloudStorage =
                new CloudStorageAccount(new StorageCredentials(storageAccountName, await GetStorageAccountPrimaryKey(credentials, storageAccountName,serviceManagementEndpoint)),storageEndpointSuffix, true);

            var blobClient = cloudStorage.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(OctopusPackagesContainerName);

            await container.CreateIfNotExistsAsync();

            var permission = await container.GetPermissionsAsync();
            permission.PublicAccess = BlobContainerPublicAccessType.Off;
            await container.SetPermissionsAsync(permission);

            var fileInfo = new FileInfo(packageFile);

            var packageBlob = GetUniqueBlobName(uploadedFileName, fileInfo, container);
            if (await packageBlob.ExistsAsync())
            {
                log.VerboseFormat("A blob named {0} already exists with the same length, so it will be used instead of uploading the new package.",
                    packageBlob.Name);
                return packageBlob.Uri;
            }

            await UploadBlobInChunks(fileInfo, packageBlob, blobClient);

            log.Info("Package upload complete");
            return packageBlob.Uri;
        }

        CloudBlockBlob GetUniqueBlobName(string uploadedFileName, FileInfo fileInfo, CloudBlobContainer container)
        {
            var length = fileInfo.Length;
            var packageBlob = Uniquifier.UniquifyUntil(
                uploadedFileName,
                container.GetBlockBlobReference,
                blob =>
                {
                    if (blob.Exists() && blob.Properties.Length != length)
                    {
                        log.Verbose("A blob named " + blob.Name + " already exists but has a different length.");
                        return true;
                    }

                    return false;
                });

            return packageBlob;
        }

        async Task UploadBlobInChunks(FileInfo fileInfo, CloudBlockBlob packageBlob, CloudBlobClient blobClient)
        {
            var operationContext = new OperationContext();
            operationContext.ResponseReceived += delegate(object sender, RequestEventArgs args)
            {
                var statusCode = (int) args.Response.StatusCode;
                var statusDescription = args.Response.StatusDescription;
                log.Verbose($"Uploading, response received: {statusCode} {statusDescription}");
                if (statusCode >= 400)
                {
                    log.Error("Error when uploading the package. Azure returned a HTTP status code of: " +
                              statusCode + " " + statusDescription);
                    log.Verbose("The upload will be retried");
                }
            };

            await blobClient.SetServicePropertiesAsync(blobClient.GetServiceProperties(), new BlobRequestOptions(), operationContext);

            log.VerboseFormat("Uploading the package to blob storage. The package file is {0}.", fileInfo.Length.ToFileSizeString());

            using (var fileReader = fileInfo.OpenRead())
            {
                var blocklist = new List<string>();

                long uploadedSoFar = 0;

                var data = new byte[1024 * 1024];
                var id = 1;

                while (true)
                {
                    id++;

                    var read = await fileReader.ReadAsync(data, 0, data.Length);
                    if (read == 0)
                    {
                        await packageBlob.PutBlockListAsync(blocklist);
                        break;
                    }

                    var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(id.ToString(CultureInfo.InvariantCulture).PadLeft(30, '0')));
                    await packageBlob.PutBlockAsync(blockId, new MemoryStream(data, 0, read, true), null);
                    blocklist.Add(blockId);

                    uploadedSoFar += read;

                    log.Progress((int) ((uploadedSoFar * 100)/fileInfo.Length), "Uploading package to blob storage");
                    log.VerboseFormat("Uploading package to blob storage: {0} of {1}", uploadedSoFar.ToFileSizeString(), fileInfo.Length.ToFileSizeString());
                }
            }

            log.Verbose("Upload complete");
        }

        async Task<string> GetStorageAccountPrimaryKey(SubscriptionCloudCredentials credentials, string storageAccountName,string serviceManagementEndpoint)
        {
            var httpClientProxy = new AuthHttpClientFactory().GetHttpClient();
            using var cloudClient = new StorageManagementClient(credentials, new Uri(serviceManagementEndpoint), httpClientProxy);
            var getKeysResponse = await cloudClient.StorageAccounts.GetKeysAsync(storageAccountName);

            if (getKeysResponse.StatusCode != HttpStatusCode.OK)
                throw new Exception($"GetKeys for storage-account {storageAccountName} returned HTTP status-code {getKeysResponse.StatusCode}");

            return getKeysResponse.PrimaryKey;
        }
    }
}