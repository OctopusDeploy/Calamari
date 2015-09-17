using System;
using Microsoft.WindowsAzure;

namespace Calamari.Azure.Integration
{
    public interface IAzurePackageUploader
    {
        Uri Upload(SubscriptionCloudCredentials credentials, string storageAccountName, string packageFile, string uploadedFileName);
    }
}