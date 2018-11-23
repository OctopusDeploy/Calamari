using System.Xml.Linq;
using Calamari.Azure.Accounts;
using Calamari.Integration.Certificates;
using Microsoft.WindowsAzure.Management.Compute.Models;

namespace Calamari.Azure.Integration
{
    public interface IAzureCloudServiceConfigurationRetriever
    {
        XDocument GetConfiguration(ICertificateStore certificateStore, AzureAccount account, string serviceName, DeploymentSlot slot);
    }
}