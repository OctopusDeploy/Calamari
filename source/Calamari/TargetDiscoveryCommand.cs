using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.AzureAppService.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace Calamari.AzureAppService
{
    [Command("target-discovery", Description = "Discover Azure web applications")]
    public class TargetDiscoveryCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<TargetDiscoveryBehaviour>();
        }
    }

    class TargetDiscoveryBehaviour : IDeployBehaviour
    {
        public bool IsEnabled(RunningDeployment context) => true;

        public async Task Execute(RunningDeployment runningDeployment)
        {
            await Task.CompletedTask;
            var targetDiscoveryScope = GetTargetDiscoveryContext(runningDeployment.Variables);
            var account = ServicePrincipalAccount.CreateFromTargetDiscoveryScope(targetDiscoveryScope.Account);
            var azureClient = account.CreateAzureClient();

            var webApps = azureClient.WebApps.ListWebAppBasic();

            foreach (var webApp in webApps)
            {
                // TODO: Trigger service message with details of discovered target.
                foreach (var tag in webApp.Tags)
                {
                    Console.WriteLine($"{tag.Key}: {tag.Value}");
                }
            }
        }

        private TargetDiscoveryContext GetTargetDiscoveryContext(IVariables variables)
        {
            var json = variables.Get("Octopus.TargetDiscovery.Context");
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<TargetDiscoveryContext>(json, options);
        }
    }
}