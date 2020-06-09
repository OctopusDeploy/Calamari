using System;
using System.Linq;
using Calamari.Commands.Support;
using Microsoft.WindowsAzure.Management.Compute;

namespace Calamari.AzureCloudService
{
    [Command("health-check", Description = "Run a health check on a DeploymentTargetType")]
    public class HealthCheckCommand : ICommand
    {
        readonly IVariables variables;

        public HealthCheckCommand(IVariables variables)
        {
            this.variables = variables;
        }

        public int Execute()
        {
            var account = new AzureAccount(variables);
            var cloudServiceName = variables.Get(SpecialVariables.Action.Azure.CloudServiceName);
            var certificate = CalamariCertificateStore.GetOrAdd(account.CertificateThumbprint, account.CertificateBytes);

            using (var azureClient = account.CreateComputeManagementClient(certificate))
            {
                var azureResponse = azureClient.HostedServices.List();
                var hostedService = azureResponse.HostedServices.FirstOrDefault(hs => hs.ServiceName == cloudServiceName);

                if (hostedService == null)
                {
                    throw new Exception($"Hosted service with name {cloudServiceName} was not found.");
                }
            }

            return 0;
        }
    }
}