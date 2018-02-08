using System;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Calamari.Azure.Deployment.Integration.ResourceGroups;
using Calamari.Azure.Integration;
using Calamari.Azure.Integration.Security;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.ResourceManager.Models;
using Microsoft.Rest;
using Calamari.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octostache;

namespace Calamari.Azure.Deployment.Conventions
{
    public class DeployAzureResourceGroupConvention : IInstallConvention
    {
        private static readonly ITemplateReplacement TemplateReplacement = new TemplateReplacement();
        
        readonly string templateFile;
        readonly string templateParametersFile;
        private readonly bool filesInPackage;
        readonly ICalamariFileSystem fileSystem;
        readonly IResourceGroupTemplateNormalizer parameterNormalizer;

        public DeployAzureResourceGroupConvention(string templateFile, string templateParametersFile, bool filesInPackage, 
            ICalamariFileSystem fileSystem, IResourceGroupTemplateNormalizer parameterNormalizer)
        {
            this.templateFile = templateFile;
            this.templateParametersFile = templateParametersFile;
            this.filesInPackage = filesInPackage;
            this.fileSystem = fileSystem;
            this.parameterNormalizer = parameterNormalizer;
        }

        public void Install(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var subscriptionId = variables[SpecialVariables.Action.Azure.SubscriptionId];
            var tenantId = variables[SpecialVariables.Action.Azure.TenantId];
            var clientId = variables[SpecialVariables.Action.Azure.ClientId];
            var password = variables[SpecialVariables.Action.Azure.Password];
            var resourceManagementEndpoint = variables.Get(SpecialVariables.Action.Azure.ResourceManagementEndPoint, DefaultVariables.ResourceManagementEndpoint);
            if (resourceManagementEndpoint != DefaultVariables.ResourceManagementEndpoint)
                Log.Info("Using override for resource management endpoint - {0}", resourceManagementEndpoint);

            var activeDirectoryEndPoint = variables.Get(SpecialVariables.Action.Azure.ActiveDirectoryEndPoint, DefaultVariables.ActiveDirectoryEndpoint);
            if (activeDirectoryEndPoint != DefaultVariables.ActiveDirectoryEndpoint)
                Log.Info("Using override for Azure Active Directory endpoint - {0}", activeDirectoryEndPoint);

            var resourceGroupName = variables[SpecialVariables.Action.Azure.ResourceGroupName];
            var deploymentName = !string.IsNullOrWhiteSpace(variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentName])
                    ? variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentName]
                    : GenerateDeploymentNameFromStepName(variables[SpecialVariables.Action.Name]);
            var deploymentMode = (DeploymentMode) Enum.Parse(typeof (DeploymentMode),
                variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentMode]);
            var template = TemplateReplacement.ResolveAndSubstituteFile(fileSystem, templateFile, filesInPackage, variables); 
            var parameters = !string.IsNullOrWhiteSpace(templateParametersFile) 
                ? parameterNormalizer.Normalize(TemplateReplacement.ResolveAndSubstituteFile(fileSystem, templateParametersFile, filesInPackage, variables))
                : null;

            Log.Info($"Deploying Resource Group {resourceGroupName} in subscription {subscriptionId}.\nDeployment name: {deploymentName}\nDeployment mode: {deploymentMode}");

            // We re-create the client each time it is required in order to get a new authorization-token. Else, the token can expire during long-running deployments.
            Func<IResourceManagementClient> createArmClient = () =>
            {
                var token = new TokenCredentials(ServicePrincipal.GetAuthorizationToken(tenantId, clientId, password, resourceManagementEndpoint, activeDirectoryEndPoint));
                var resourcesClient = new ResourceManagementClient(token)
                {
                    SubscriptionId = subscriptionId,
                    BaseUri = new Uri(resourceManagementEndpoint),
                };
                resourcesClient.HttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                resourcesClient.HttpClient.BaseAddress = new Uri(resourceManagementEndpoint);
                return resourcesClient;
            };

            CreateDeployment(createArmClient, resourceGroupName, deploymentName, deploymentMode, template, parameters);
            PollForCompletion(createArmClient, resourceGroupName, deploymentName, variables);
        }

        protected static string GenerateDeploymentNameFromStepName(string stepName)
        {
            var deploymentName = stepName ?? string.Empty;
            deploymentName = deploymentName.ToLower();
            deploymentName = Regex.Replace(deploymentName, "\\s", "-");
            deploymentName = new string(deploymentName.Select(x => (char.IsLetterOrDigit(x) || x == '-') ? x : '-').ToArray());
            deploymentName = Regex.Replace(deploymentName, "-+", "-");
            deploymentName = deploymentName.Trim('-', '/');
            // Azure Deployment Namese can only be 64 characters == 31 chars + "-" (1) + Guid (32 chars)
            deploymentName = deploymentName.Length <= 31 ? deploymentName : deploymentName.Substring(0, 31);
            deploymentName = deploymentName + "-" + Guid.NewGuid().ToString("N");
            return deploymentName;
        }

        static void CreateDeployment(Func<IResourceManagementClient> createArmClient, string resourceGroupName, string deploymentName,
            DeploymentMode deploymentMode, string template, string parameters)
        {
            Log.Verbose($"Template:\n{template}\n");
            if (parameters != null)
            {
               Log.Verbose($"Parameters:\n{parameters}\n"); 
            }

            using (var armClient = createArmClient())
            {
                var createDeploymentResult = armClient.Deployments.CreateOrUpdate(resourceGroupName, deploymentName,
                    new Microsoft.Azure.Management.ResourceManager.Models.Deployment
                    {
                        Properties = new DeploymentProperties
                        {
                            Mode = deploymentMode,
                            Template = template,
                            Parameters = parameters
                        }
                    });

                Log.Info($"Deployment created: {createDeploymentResult.Id}");
            }
        }

        static void PollForCompletion(Func<IResourceManagementClient> createArmClient, string resourceGroupName,
            string deploymentName, VariableDictionary variables)
        {
            // While the deployment is running, we poll to check its state.
            // We increase the poll interval according to the Fibonacci sequence, up to a maximum of 30 seconds. 
            var currentPollWait = 1;
            var previousPollWait = 0;
            var continueToPoll = true;
            const int maxWaitSeconds = 30;

            while (continueToPoll)
            {
                Thread.Sleep(TimeSpan.FromSeconds(Math.Min(currentPollWait, maxWaitSeconds)));

                Log.Verbose("Polling for status of deployment...");
                using (var armClient = createArmClient())
                {
                    var deployment = armClient.Deployments.Get(resourceGroupName, deploymentName);
                    if (deployment.Properties == null)
                    {
                        Log.Verbose($"Failed to find deployment.Properties");
                        return;
                    }

                    Log.Verbose($"Provisioning state: {deployment.Properties.ProvisioningState}");
                    switch (deployment.Properties.ProvisioningState)
                    {
                        case "Succeeded":
                            Log.Info($"Deployment {deploymentName} complete.");
                            Log.Info(GetOperationResults(armClient, resourceGroupName, deploymentName));
                            CaptureOutputs(deployment.Properties.Outputs?.ToString(), variables);
                            continueToPoll = false;
                            break;

                        case "Failed":
                            throw new CommandException($"Azure Resource Group deployment {deploymentName} failed:\n" +
                                                       GetOperationResults(armClient, resourceGroupName, deploymentName));

                        case "Canceled":
                            throw new CommandException($"Azure Resource Group deployment {deploymentName} was canceled:\n" +
                                                       GetOperationResults(armClient, resourceGroupName, deploymentName));

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

        static void CaptureOutputs(string outputsJson, VariableDictionary variables)
        {
            if (string.IsNullOrWhiteSpace(outputsJson))
                return;

            Log.Verbose("Deployment Outputs:");
            Log.Verbose(outputsJson);

            var outputs = JObject.Parse(outputsJson);

            foreach (var output in outputs)
            {
                Log.SetOutputVariable($"AzureRmOutputs[{output.Key}]", output.Value["value"].ToString(), variables);
            }
        }
    }
}