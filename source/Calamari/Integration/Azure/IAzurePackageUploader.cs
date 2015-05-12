using System;
using Microsoft.WindowsAzure;

namespace Calamari.Integration.Azure
{
    public interface IAzurePackageUploader
    {
        Uri Upload(SubscriptionCloudCredentials credentials, string storageAccountName, string packageFile, string uploadedFileName);
    }
}