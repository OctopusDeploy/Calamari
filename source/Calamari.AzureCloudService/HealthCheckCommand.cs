using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Hyak.Common;
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
                try
                {
                    azureClient.HostedServices.Get(cloudServiceName);
                }
                catch (CloudException e)
                {
                    if (e.Error.Code == "ResourceNotFound")
                    {
                        throw new Exception($"Hosted service with name {cloudServiceName} was not found.");
                    }

                    throw;
                }
            }

            return 0;
        }
    }
}