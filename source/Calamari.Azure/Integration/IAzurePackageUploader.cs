using System;
using Microsoft.Azure;

namespace Calamari.Azure.Integration
{
    public interface IAzurePackageUploader
    {
        Uri Upload(SubscriptionCloudCredentials credentials, string storageAccountName, string packageFile, string uploadedFileName, string storageEndpointSuffix, string serviceManagementEndpoint);
    }
}