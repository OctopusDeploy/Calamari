using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.ResourceManager.Models;
using Microsoft.Rest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octopus.CoreUtilities.Extensions;
using AzureResourceManagerDeployment = Microsoft.Azure.Management.ResourceManager.Models.Deployment;

namespace Calamari.AzureResourceGroup
{
    class DeployAzureResourceGroupBehaviour : IDeployBehaviour
    {
        readonly TemplateService templateService;
        readonly IResourceGroupTemplateNormalizer parameterNormalizer;
        readonly ILog log;

        public DeployAzureResourceGroupBehaviour(TemplateService templateService, IResourceGroupTemplateNormalizer parameterNormalizer, ILog log)
        {
            this.templateService = templateService;
            this.parameterNormalizer = parameterNormalizer;
            this.log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var subscriptionId = variables[AzureAccountVariables.SubscriptionId];
            var tenantId = variables[AzureAccountVariables.TenantId];
            var clientId = variables[AzureAccountVariables.ClientId];
            var password = variables[AzureAccountVariables.Password];
            var jwt = variables[AzureAccountVariables.Jwt];

            var templateFile = variables.Get(SpecialVariables.Action.Azure.Template, "template.json");
            var templateParametersFile = variables.Get(SpecialVariables.Action.Azure.TemplateParameters, "parameters.json");
            var filesInPackage = variables.Get(SpecialVariables.Action.Azure.TemplateSource, String.Empty) == "Package";
            if (filesInPackage)
            {
                templateFile = variables.Get(SpecialVariables.Action.Azure.ResourceGroupTemplate);
                templateParametersFile = variables.Get(SpecialVariables.Action.Azure.ResourceGroupTemplateParameters);
            }
            var resourceManagementEndpoint = variables.Get(AzureAccountVariables.ResourceManagementEndPoint, DefaultVariables.ResourceManagementEndpoint);

            if (resourceManagementEndpoint != DefaultVariables.ResourceManagementEndpoint)
                log.InfoFormat("Using override for resource management endpoint - {0}", resourceManagementEndpoint);

            var activeDirectoryEndPoint = variables.Get(AzureAccountVariables.ActiveDirectoryEndPoint, DefaultVariables.ActiveDirectoryEndpoint);
                log.InfoFormat("Using override for Azure Active Directory endpoint - {0}", activeDirectoryEndPoint);

            var resourceGroupName = variables[SpecialVariables.Action.Azure.ResourceGroupName];
            var deploymentName = !string.IsNullOrWhiteSpace(variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentName])
                ? variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentName]
                : GenerateDeploymentNameFromStepName(variables[ActionVariables.Name]);
            var deploymentMode = (DeploymentMode) Enum.Parse(typeof (DeploymentMode),
                                                             variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentMode]);
            var template = templateService.GetSubstitutedTemplateContent(templateFile, filesInPackage, variables);
            var parameters = !string.IsNullOrWhiteSpace(templateParametersFile)
                ? parameterNormalizer.Normalize(templateService.GetSubstitutedTemplateContent(templateParametersFile, filesInPackage, variables))
                : null;

            log.Info($"Deploying Resource Group {resourceGroupName} in subscription {subscriptionId}.\nDeployment name: {deploymentName}\nDeployment mode: {deploymentMode}");

            // We re-create the client each time it is required in order to get a new authorization-token. Else, the token can expire during long-running deployments.
            Func<Task<IResourceManagementClient>> createArmClient = async () =>
                                                              {
                                                                  var token = !jwt.IsNullOrEmpty()
                                                                      ? await new AzureOidcAccount(variables).Credentials(CancellationToken.None)
                                                                      : await new AzureServicePrincipalAccount(variables).Credentials();
                                                                  var resourcesClient = new ResourceManagementClient(token, AuthHttpClientFactory.ProxyClientHandler())
                                                                  {
                                                                      SubscriptionId = subscriptionId,
                                                                      BaseUri = new Uri(resourceManagementEndpoint),
                                                                  };
                                                                  resourcesClient.HttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                                                                  resourcesClient.HttpClient.BaseAddress = new Uri(resourceManagementEndpoint);
                                                                  return resourcesClient;
                                                              };

            await CreateDeployment(createArmClient, resourceGroupName, deploymentName, deploymentMode, template, parameters);
            await PollForCompletion(createArmClient, resourceGroupName, deploymentName, variables);
        }

        internal static string GenerateDeploymentNameFromStepName(string stepName)
        {
            var deploymentName = stepName ?? string.Empty;
            deploymentName = deploymentName.ToLower();
            deploymentName = Regex.Replace(deploymentName, "\\s", "-");
            deploymentName = new string(deploymentName.Select(x => (char.IsLetterOrDigit(x) || x == '-') ? x : '-').ToArray());
            deploymentName = Regex.Replace(deploymentName, "-+", "-");
            deploymentName = deploymentName.Trim('-', '/');
            // Azure Deployment Names can only be 64 characters == 31 chars + "-" (1) + Guid (32 chars)
            deploymentName = deploymentName.Length <= 31 ? deploymentName : deploymentName.Substring(0, 31);
            deploymentName = deploymentName + "-" + Guid.NewGuid().ToString("N");
            return deploymentName;
        }

        async Task CreateDeployment(Func<Task<IResourceManagementClient>> createArmClient, string resourceGroupName, string deploymentName,
                                     DeploymentMode deploymentMode, string template, string parameters)
        {
            log.Verbose($"Template:\n{template}\n");
            if (parameters != null)
            {
                log.Verbose($"Parameters:\n{parameters}\n");
            }

            using (var armClient = await createArmClient())
            {
                try
                {
                    var createDeploymentResult = await armClient.Deployments.BeginCreateOrUpdateAsync(resourceGroupName,
                                                                                                      deploymentName,
                                                                                                      new AzureResourceManagerDeployment
                                                                                                      {
                                                                                                          Properties = new DeploymentProperties
                                                                                                          {
                                                                                                              Mode = deploymentMode,
                                                                                                              Template = template,
                                                                                                              Parameters = parameters
                                                                                                          }
                                                                                                      });

                    log.Info($"Deployment created: {createDeploymentResult.Id}");
                }
                catch (Microsoft.Rest.Azure.CloudException ex)
                {
                    log.Error("Error submitting deployment");
                    log.Error(ex.Message);
                    LogCloudError(ex.Body, 0);
                    throw;
                }
            }
        }

        async Task PollForCompletion(Func<Task<IResourceManagementClient>> createArmClient, string resourceGroupName,
                                      string deploymentName, IVariables variables)
        {
            // While the deployment is running, we poll to check its state.
            // We increase the poll interval according to the Fibonacci sequence, up to a maximum of 30 seconds.
            var currentPollWait = 1;
            var previousPollWait = 0;
            var continueToPoll = true;
            const int maxWaitSeconds = 30;

            while (continueToPoll)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(currentPollWait, maxWaitSeconds)));

                log.Verbose("Polling for status of deployment...");
                using (var armClient = await createArmClient())
                {
                    var deployment = await armClient.Deployments.GetAsync(resourceGroupName, deploymentName);
                    if (deployment.Properties == null)
                    {
                        log.Verbose("Failed to find deployment.Properties");
                        return;
                    }

                    log.Verbose($"Provisioning state: {deployment.Properties.ProvisioningState}");
                    switch (deployment.Properties.ProvisioningState)
                    {
                        case "Succeeded":
                            log.Info($"Deployment {deploymentName} complete.");
                            log.Info(GetOperationResults(armClient, resourceGroupName, deploymentName));
                            CaptureOutputs(deployment.Properties.Outputs?.ToString(), variables);
                            continueToPoll = false;
                            break;

                        case "Failed":
                            throw new CommandException($"Azure Resource Group deployment {deploymentName} failed:\n" + GetOperationResults(armClient, resourceGroupName, deploymentName));

                        case "Canceled":
                            throw new CommandException($"Azure Resource Group deployment {deploymentName} was canceled:\n" + GetOperationResults(armClient, resourceGroupName, deploymentName));

                        default:
                            if (currentPollWait < maxWaitSeconds)
                            {
                                var temp = previousPollWait;
                                previousPollWait = currentPollWait;
                                currentPollWait = temp + currentPollWait;
                            }

                            break;
                    }
                }
            }
        }

        static string GetOperationResults(IResourceManagementClient armClient, string resourceGroupName, string deploymentName)
        {
            var operations = armClient?.DeploymentOperations.List(resourceGroupName, deploymentName);
            if (operations == null)
                return null;

            var log = new StringBuilder("Operations details:\n");
            foreach (var operation in operations)
            {
                if (operation?.Properties == null)
                    continue;

                log.AppendLine($"Resource: {operation.Properties.TargetResource?.ResourceName}");
                log.AppendLine($"Type: {operation.Properties.TargetResource?.ResourceType}");
                log.AppendLine($"Timestamp: {operation.Properties.Timestamp?.ToLocalTime():s}");
                log.AppendLine($"Deployment operation: {operation.Id}");
                log.AppendLine($"Status: {operation.Properties.StatusCode}");
                log.AppendLine($"Provisioning State: {operation.Properties.ProvisioningState}");
                if (operation.Properties.StatusMessage != null)
                    log.AppendLine($"Status Message: {JsonConvert.SerializeObject(operation.Properties.StatusMessage)}");
            }

            return log.ToString();
        }

        void CaptureOutputs(string outputsJson, IVariables variables)
        {
            if (string.IsNullOrWhiteSpace(outputsJson))
                return;

            log.Verbose("Deployment Outputs:");
            log.Verbose(outputsJson);

            var outputs = JObject.Parse(outputsJson);

            foreach (var output in outputs)
            {
                log.SetOutputVariable($"AzureRmOutputs[{output.Key}]", output.Value["value"].ToString(), variables);
            }
        }

        void LogCloudError(Microsoft.Rest.Azure.CloudError error, int count)
        {
            if (count > 5)
            {
                return;
            }

            if (error != null)
            {
                string indent = new string(' ', count * 4);
                if (!string.IsNullOrEmpty(error.Message))
                {
                    log.Error($"{indent}Message: {error.Message}");
                }
                if (!string.IsNullOrEmpty(error.Code))
                {
                    log.Error($"{indent}Code: {error.Code}");
                }
                if (!string.IsNullOrEmpty(error.Target))
                {
                    log.Error($"{indent}Target: {error.Target}");
                }
                foreach (var errorDetail in error.Details)
                {
                    LogCloudError(errorDetail, ++count);
                }
            }
        }
    }
}