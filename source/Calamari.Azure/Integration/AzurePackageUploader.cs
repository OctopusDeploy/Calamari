using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using Calamari.Integration.FileSystem;
using Calamari.Util;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Management.Storage;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Calamari.Azure.Integration
{
    public class AzurePackageUploader : IAzurePackageUploader
    {
        const string OctopusPackagesContainerName = "octopuspackages";

        public Uri Upload(SubscriptionCloudCredentials credentials, string storageAccountName, string packageFile, string uploadedFileName, string storageEndpointSuffix, string serviceManagementEndpoint)
        {
            var cloudStorage =
                new CloudStorageAccount(new StorageCredentials(storageAccountName, GetStorageAccountPrimaryKey(credentials, storageAccountName,serviceManagementEndpoint)),storageEndpointSuffix, true);

            var blobClient = cloudStorage.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(OctopusPackagesContainerName);
            
            container.CreateIfNotExists();

            var permission = container.GetPermissions();
            permission.PublicAccess = BlobContainerPublicAccessType.Off;
            container.SetPermissions(permission);

            var fileInfo = new FileInfo(packageFile);

            var packageBlob = GetUniqueBlobName(uploadedFileName, fileInfo, container);
            if (packageBlob.Exists())
            {
                Log.VerboseFormat("A blob named {0} already exists with the same length, so it will be used instead of uploading the new package.",
                    packageBlob.Name);
                return packageBlob.Uri;
            }

            UploadBlobInChunks(fileInfo, packageBlob, blobClient);

            Log.Info("Package upload complete");
            return packageBlob.Uri;
        }

        static CloudBlockBlob GetUniqueBlobName(string uploadedFileName, FileInfo fileInfo, CloudBlobContainer container)
        {
            var length = fileInfo.Length;
            var packageBlob = Uniquifier.UniquifyUntil(
                uploadedFileName,
                container.GetBlockBlobReference,
                blob =>
                {
                    if (blob.Exists() && blob.Properties.Length != length)
                    {
                        Log.Verbose("A blob named " + blob.Name + " already exists but has a different length.");
                        return true;
                    }

                    return false;
                });

            return packageBlob;
        }

        static void UploadBlobInChunks(FileInfo fileInfo, CloudBlockBlob packageBlob, CloudBlobClient blobClient)
        {
            var operationContext = new OperationContext();
            operationContext.ResponseReceived += delegate(object sender, RequestEventArgs args)
            {
                var statusCode = (int) args.Response.StatusCode;
                var statusDescription = args.Response.StatusDescription;
                if (statusCode >= 400)
                {
                    Log.Error("Error when uploading the package. Azure returned a HTTP status code of: " +
                              statusCode + " " + statusDescription);
                    Log.Verbose("The upload will be retried");

                    return;
                }

                Log.Verbose("Uploading, response received: " + statusCode + " " + statusDescription);

            };

            blobClient.SetServiceProperties(blobClient.GetServiceProperties(), operationContext: operationContext); 

            Log.VerboseFormat("Uploading the package to blob storage. The package file is {0}.", fileInfo.Length.ToFileSizeString());
            
            using (var fileReader = fileInfo.OpenRead())
            {
                var blocklist = new List<string>();

                long uploadedSoFar = 0;

                var data = new byte[1024 * 1024];
                var id = 1;

                while (true)
                {
                    id++;

                    var read = fileReader.Read(data, 0, data.Length);
                    if (read == 0)
                    {
                        packageBlob.PutBlockList(blocklist);

                        break;
                    }
                    
                    var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(id.ToString(CultureInfo.InvariantCulture).PadLeft(30, '0')));
                    packageBlob.PutBlock(blockId, new MemoryStream(data, 0, read, true), null);
                    blocklist.Add(blockId);

                    uploadedSoFar += read;

                    Log.ServiceMessages.Progress((int) ((uploadedSoFar * 100)/fileInfo.Length), "Uploading package to blob storage");
                    Log.VerboseFormat("Uploading package to blob storage: {0} of {1}", uploadedSoFar.ToFileSizeString(), fileInfo.Length.ToFileSizeString());
                }
            }

            Log.Verbose("Upload complete");
        }

        static string GetStorageAccountPrimaryKey(SubscriptionCloudCredentials credentials, string storageAccountName,string serviceManagementEndpoint)
        {
            using (var cloudClient = new StorageManagementClient(credentials, new Uri(serviceManagementEndpoint)))
            {
                var getKeysResponse = cloudClient.StorageAccounts.GetKeys(storageAccountName);

                if (getKeysResponse.StatusCode != HttpStatusCode.OK)
                    throw new Exception(string.Format("GetKeys for storage-account {0} returned HTTP status-code {1}", storageAccountName, getKeysResponse.StatusCode));

                return getKeysResponse.PrimaryKey;
            }
        }
    }
}