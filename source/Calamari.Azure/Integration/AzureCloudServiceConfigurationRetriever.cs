using System;
using System.Net;
using System.Xml.Linq;
using Calamari.Azure.Accounts;
using Calamari.Integration.Certificates;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.Management.Compute.Models;

namespace Calamari.Azure.Integration
{
    public class AzureCloudServiceConfigurationRetriever : IAzureCloudServiceConfigurationRetriever
    {
        public XDocument GetConfiguration(ICertificateStore certificateStore, AzureAccount account, string serviceName, DeploymentSlot slot)
        {
            using (var client = account.CreateComputeManagementClient(certificateStore))
            {
                try
                {
                    var response = client.Deployments.GetBySlot(serviceName, slot);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception(string.Format("Getting deployment by slot returned HTTP Status Code: {0}",
                            response.StatusCode));
                    }

                    return string.IsNullOrEmpty(response.Configuration)
                        ? null
                        : XDocument.Parse(response.Configuration);
                }
                catch (CloudException cloudException)
                {
                    Log.VerboseFormat("Getting deployments for service '{0}', slot {1}, returned:\n{2}", serviceName,
                        slot.ToString(), cloudException.Message);
                    return null;
                }
                catch (Hyak.Common.CloudException exception)
                {
                    Log.VerboseFormat("Getting deployments for service '{0}', slot {1}, returned:\n{2}", serviceName,
                        slot.ToString(), exception.Message);
                    return null;
                }
            }
        }
    }
}