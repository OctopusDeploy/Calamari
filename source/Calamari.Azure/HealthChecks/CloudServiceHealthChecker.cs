using System;
using System.Linq;
using System.Net;
using Calamari.Azure.Accounts;
using Calamari.Deployment;
using Calamari.HealthChecks;
using Calamari.Integration.Certificates;
using Calamari.Integration.Processes;
using Microsoft.WindowsAzure.Management.Compute;

namespace Calamari.Azure.HealthChecks
{
    public class CloudServiceHealthChecker : IDoesDeploymentTargetTypeHealthChecks
    {
        private readonly ILog log;
        private readonly ICertificateStore certificateStore;

        public CloudServiceHealthChecker(ILog log, ICertificateStore certificateStore)
        {
            this.log = log;
            this.certificateStore = certificateStore;
        }

        public bool HandlesDeploymentTargetTypeName(string deploymentTargetTypeName)
        {
            return deploymentTargetTypeName == "AzureCloudService";
        }

        public int ExecuteHealthCheck(IVariables variables)
        {
            var account = AccountFactory.Create(variables);

            var cloudServiceName = variables.Get(SpecialVariables.Action.Azure.CloudServiceName);

            if (account is AzureAccount azureAccount)
            {
                using (var azureClient = azureAccount.CreateComputeManagementClient(certificateStore))
                {
                    var azureResponse = azureClient.HostedServices.List();
                    if (azureResponse.StatusCode != HttpStatusCode.OK)
                        throw new Exception("Azure returned HTTP status-code " + azureResponse.StatusCode);

                    var hostedService = azureResponse.HostedServices.FirstOrDefault(hs => hs.ServiceName == cloudServiceName);
                    if (hostedService == null)
                        throw new Exception($"Hosted service with name {cloudServiceName} was not found.");
                }
            }
            else if (account is AzureServicePrincipalAccount servicePrincipalAccount)
            {
                throw new Exception($"Cloud service targets cannot use Service Principal accounts, a Management Certificate account is required.");
            }

            return 0;
        }
    }
}