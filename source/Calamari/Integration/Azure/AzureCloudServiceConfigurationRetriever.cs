using System;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute.Models;

namespace Calamari.Integration.Azure
{
    public class AzureCloudServiceConfigurationRetriever : IAzureCloudServiceConfigurationRetriever
    {
        public XDocument GetConfiguration(SubscriptionCloudCredentials credentials, string serviceName, DeploymentSlot slot)
        {
            using (var client = CloudContext.Clients.CreateComputeManagementClient(credentials))
            {
                var response = client.Deployments.GetBySlot(serviceName, slot);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception(string.Format("Getting deployment by slot returned HTTP Status Code: {0}", response.StatusCode));
                }

                return string.IsNullOrEmpty(response.Configuration)
                    ? null
                    : XDocument.Parse(response.Configuration);
            }
        }
    }
}