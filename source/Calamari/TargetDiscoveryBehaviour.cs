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
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.AppService.Fluent;

#nullable enable
namespace Calamari.AzureAppService
{
    public class TargetDiscoveryBehaviour : IDeployBehaviour
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
            if (targetDiscoveryContext == null)
            {
                Log.Warn("Aborting target discovery.");
                return;
            }

            var account = targetDiscoveryContext.Authentication.AccountDetails;
            var azureClient = account.CreateAzureClient();

            try
            {
                var webApps = azureClient.WebApps.ListWebAppBasic();
                foreach (var webApp in webApps)
                {
                    var tags = GetTags(webApp);
                    var matchResult = targetDiscoveryContext.Scope.Match(tags);
                    if (matchResult.IsSuccess)
                    {
                        Log.Info($"Discovered matching web app: {webApp.Name}");
                        WriteTargetCreationServiceMessage(
                            webApp, targetDiscoveryContext, matchResult, runningDeployment.Variables);
                    }
                }

                if (!webApps.Any())
                {
                    Log.Info("No matching web apps found.");
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Error connecting to Azure to look for web apps:");
                Log.Warn(ex.Message);
                Log.Warn("Aborting target discovery.");
            }
        }

        private TargetTags GetTags(IWebAppBasic webApp)
        {
            webApp.Tags.TryGetValue(TargetTags.EnvironmentTagName, out string? environment);
            webApp.Tags.TryGetValue(TargetTags.RoleTagName, out string? role);
            webApp.Tags.TryGetValue(TargetTags.ProjectTagName, out string? project);
            webApp.Tags.TryGetValue(TargetTags.SpaceTagName, out string? space);
            webApp.Tags.TryGetValue(TargetTags.TenantTagName, out string? tenant);
            return new TargetTags(
                environment: environment,
                role: role,
                project: project,
                space: space,
                tenant: tenant);
        }

        private void WriteTargetCreationServiceMessage(
            IWebAppBasic webApp,
            TargetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>> context,
            TargetMatchResult matchResult,
            IVariables variables)
        {
            // TODO: extend target discovery context to include account ID and worker pool ID directly?
            // TODO: confirm what name to use (key? for immutable matching of existing targets?)
            // TODO: handle web app slots.
            var parameters = new Dictionary<string, string?> {
                    { "name", webApp.Name },
                    { "azureWebApp", "webApp.Name" },
                    { "azureResourceGroupName", webApp.ResourceGroupName },
                    { "octopusAccountIdOrName", context.Authentication.AccountId },
                    { "octopusRoles", matchResult.Role },
                    { "updateIfExisting", "True" },
                    { "octopusDefaultWorkerPoolIdOrName", context.Scope!.WorkerPoolId },
                };

            var serviceMessage = new ServiceMessage(
                "create-azurewebapptarget",
                parameters.Where(p => p.Value != null).ToDictionary<KeyValuePair<string, string?>, string, string>(p => p.Key, p => p.Value!));
            Log.WriteServiceMessage(serviceMessage);
            ////Log.Info($"##octopus[create-azurewebapptarget "
            ////    + $"name=\"{AbstractLog.ConvertServiceMessageValue(webApp.Name ?? "")}\" "
            ////    + $"azureWebApp=\"{AbstractLog.ConvertServiceMessageValue(webApp.Name ?? "")}\" "
            ////    + $"azureWebAppSlot=\"\" "
            ////    + $"azureResourceGroupName=\"{AbstractLog.ConvertServiceMessageValue(webApp.ResourceGroupName ?? "")}\" "
            ////    + $"octopusAccountIdOrName=\"{AbstractLog.ConvertServiceMessageValue(context.Authentication.AccountId ?? "")}\" "
            ////    + $"octopusRoles=\"{AbstractLog.ConvertServiceMessageValue(matchResult.Role)}\" "
            ////    + $"updateIfExisting=\"{AbstractLog.ConvertServiceMessageValue("True")}\" "
            ////    + $"octopusDefaultWorkerPoolIdOrName=\"{AbstractLog.ConvertServiceMessageValue(context.Scope.WorkerPoolId ?? "")}\" ]");

        }

        private TargetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>>? GetTargetDiscoveryContext(
            IVariables variables)
        {
            const string contextVariableName = "Octopus.TargetDiscovery.Context";
            var json = variables.Get(contextVariableName);
            if (json == null)
            {
                return null;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            try
            {
                return JsonSerializer
                    .Deserialize<TargetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>>>(
                        json, options);
            }
            catch (JsonException ex)
            {
                Log.Warn($"Target discovery context from variable {contextVariableName} is in wrong format: {ex.Message}");
                return null;
            }
        }
    }
}