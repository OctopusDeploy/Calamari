using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;
using Hyak.Common;
using Microsoft.WindowsAzure.Management.Compute;

namespace Calamari.AzureCloudService
{
    [Command("health-check", Description = "Run a health check on a DeploymentTargetType")]
    public class HealthCheckCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<HealthCheckBehaviour>();
        }
    }

    class HealthCheckBehaviour : IDeployBehaviour
    {
        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment context)
        {
            var account = new AzureAccount(context.Variables);
            var cloudServiceName = context.Variables.Get(SpecialVariables.Action.Azure.CloudServiceName);
            var certificate = CalamariCertificateStore.GetOrAdd(account.CertificateThumbprint, account.CertificateBytes);

            using (var azureClient = account.CreateComputeManagementClient(certificate))
            {
                try
                {
                    await azureClient.HostedServices.GetAsync(cloudServiceName);
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
        }
    }
}