using System.Xml.Linq;
using Calamari.Shared;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Management.Compute.Models;

namespace Calamari.Azure.Integration
{
    public interface IAzureCloudServiceConfigurationRetriever
    {
        XDocument GetConfiguration(SubscriptionCloudCredentials credentials, string serviceName, DeploymentSlot slot);
    }
}