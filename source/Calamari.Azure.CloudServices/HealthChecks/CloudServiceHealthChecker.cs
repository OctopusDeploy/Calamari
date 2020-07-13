using System;
using System.Linq;
using System.Net;
using Calamari.Azure.CloudServices.Accounts;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.HealthChecks;
using Calamari.Integration.Certificates;
using Calamari.Integration.Processes;
using Microsoft.WindowsAzure.Management.Compute;

namespace Calamari.Azure.CloudServices.HealthChecks
{
    public class CloudServiceHealthChecker : IDoesDeploymentTargetTypeHealthChecks
    {
        private readonly ICertificateStore certificateStore;
        readonly IVariables variables;

        public CloudServiceHealthChecker(ICertificateStore certificateStore, IVariables variables)
        {
            this.certificateStore = certificateStore;
            this.variables = variables;
        }

        public bool HandlesDeploymentTargetTypeName(string deploymentTargetTypeName)
        {
            return deploymentTargetTypeName == "AzureCloudService";
        }

        public int ExecuteHealthCheck()
        {
            var account = new AzureAccount(variables);

            var cloudServiceName = variables.Get(SpecialVariables.Action.Azure.CloudServiceName);

            using (var azureClient = account.CreateComputeManagementClient(certificateStore))
            {
                var azureResponse = azureClient.HostedServices.List();
                if (azureResponse.StatusCode != HttpStatusCode.OK)
                    throw new Exception("Azure returned HTTP status-code " + azureResponse.StatusCode);

                var hostedService = azureResponse.HostedServices.FirstOrDefault(hs => hs.ServiceName == cloudServiceName);
                if (hostedService == null)
                    throw new Exception($"Hosted service with name {cloudServiceName} was not found.");
            }

            return 0;
        }
    }
}