using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Calamari.Azure;
////using Calamari.AzureAppService.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.AppService.Fluent;
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
        private ILog Log { get; }

        public TargetDiscoveryBehaviour(ILog log)
        {
            Log = log;
        }

        public bool IsEnabled(RunningDeployment context) => true;

        public async Task Execute(RunningDeployment runningDeployment)
        {
            await Task.CompletedTask;
            var targetDiscoveryContext = GetTargetDiscoveryContext(runningDeployment.Variables);
            var account = targetDiscoveryContext.Authentication.AccountDetails;
            var azureClient = account.CreateAzureClient();

            var webApps = azureClient.WebApps.ListWebAppBasic();

            foreach (var webApp in webApps.Where(app => WebAppHasMatchesScope(app, targetDiscoveryContext.Scope)))
            {
                WriteTargetCreationServiceMessage(webApp, targetDiscoveryContext, runningDeployment.Variables);
            }
        }

        // TODO: Proper tag matching (tag names, case-sensitivity etc.)
        private bool WebAppHasMatchesScope(
            IWebAppBasic webApp,
            TargetDiscoveryScope scope) =>
            webApp.Tags.Any(tag => tag.Key == "project" && tag.Value == scope.ProjectId) &&
            webApp.Tags.Any(tag => tag.Key == "environment" && tag.Value == scope.EnvironmentId) &&
            webApp.Tags.Any(tag => tag.Key == "role" && scope.Roles.Any(role => role == tag.Value));

        private void WriteTargetCreationServiceMessage(
            IWebAppBasic webApp,
            TargetDiscoveryContext<AuthenticationAccount<ServicePrincipalAccount>> context,
            IVariables variables)
        {
            // TODO: extend target discovery context to include account ID and worker pool ID directly?
            // TODO: confirm what name to use (key? for immutable matching of existing targets?)
            // TODO: Which role to use (all roles which matched in many-to-many matching?)
            var role = webApp.Tags.First(tag => tag.Key == "role" && context.Scope.Roles.Any(role => role == tag.Value)).Value;
            Log.Info($"##octopus[create-azurewebapptarget "
                + $"name=\"{AbstractLog.ConvertServiceMessageValue(webApp.Name ?? "")}\" "
                + $"azureWebApp=\"{AbstractLog.ConvertServiceMessageValue(webApp.Name ?? "")}\" "
                + $"azureWebAppSlot=\"\" "
                + $"azureResourceGroupName=\"{AbstractLog.ConvertServiceMessageValue(webApp.ResourceGroupName ?? "")}\" "
                + $"octopusAccountIdOrName=\"{AbstractLog.ConvertServiceMessageValue(context.Authentication.AccountId ?? "")}\" "
                + $"octopusRoles=\"{AbstractLog.ConvertServiceMessageValue(role)}\" "
                + $"updateIfExisting=\"{AbstractLog.ConvertServiceMessageValue("True")}\" "
                + $"octopusDefaultWorkerPoolIdOrName=\"{AbstractLog.ConvertServiceMessageValue(context.Scope.WorkerPoolId ?? "")}\" ]");

        }

        private TargetDiscoveryContext<AuthenticationAccount<ServicePrincipalAccount>> GetTargetDiscoveryContext(IVariables variables)
        {
            var json = variables.Get("Octopus.TargetDiscovery.Context");
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<TargetDiscoveryContext<AuthenticationAccount<ServicePrincipalAccount>>>(json, options);
        }
    }
}