using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace Calamari.Azure.Integration
{
    public static class DefaultVariables
    {
        public static readonly string ResourceManagementEndpoint = AzureEnvironment.AzureGlobalCloud.ResourceManagerEndpoint;
        public static readonly string ServiceManagementEndpoint =  AzureEnvironment.AzureGlobalCloud.ManagementEndpoint;
        public static readonly string ActiveDirectoryEndpoint = AzureEnvironment.AzureGlobalCloud.AuthenticationEndpoint;
        public static readonly string StorageEndpointSuffix = AzureEnvironment.AzureGlobalCloud.StorageEndpointSuffix;
    }
}
