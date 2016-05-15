using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Calamari.Azure.Deployment.Integration;
using Calamari.Azure.Deployment.Integration.ResourceGroups;
using Calamari.Azure.Integration.Security;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Util;
using Microsoft.Azure;
using Microsoft.Azure.Management.Resources;
using Microsoft.Azure.Management.Resources.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octostache;

namespace Calamari.Azure.Deployment.Conventions
{
    public class DeployAzureResourceGroupConvention : IInstallConvention
    {
        readonly string templateFile;
        readonly string templateParametersFile;
        readonly ICalamariFileSystem fileSystem;
        readonly IResourceGroupTemplateParameterParser parameterParser;

        public DeployAzureResourceGroupConvention(string templateFile, string templateParametersFile, 
            ICalamariFileSystem fileSystem, IResourceGroupTemplateParameterParser parameterParser)
        {
            this.templateFile = templateFile;
            this.templateParametersFile = templateParametersFile;
            this.fileSystem = fileSystem;
            this.parameterParser = parameterParser;
        }

        public void Install(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var subscriptionId = variables[SpecialVariables.Action.Azure.SubscriptionId];
            var tenantId = variables[SpecialVariables.Action.Azure.TenantId];
            var clientId = variables[SpecialVariables.Action.Azure.ClientId];
            var password = variables[SpecialVariables.Action.Azure.Password];
            var resourceGroupName = variables[SpecialVariables.Action.Azure.ResourceGroupName];
            var deploymentName = !string.IsNullOrWhiteSpace(variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentName])
                    ? variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentName]
                    : GenerateDeploymentNameFromStepName(variables[SpecialVariables.Action.Name]);
            var deploymentMode = (DeploymentMode) Enum.Parse(typeof (DeploymentMode),
                variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentMode]);
            var template = variables.Evaluate(fileSystem.ReadFile(templateFile));
            var parameters = !string.IsNullOrWhiteSpace(templateParametersFile) 
                ? parameterParser.ParseParameters(variables.Evaluate(fileSystem.ReadFile(templateParametersFile)))
                : null;

            Log.Info(
                $"Deploying Resource Group {resourceGroupName} in subscription {subscriptionId}.\nDeployment name: {deploymentName}\nDeployment mode: {deploymentMode}");

            // We re-create the client each time it is required in order to get a new authorization-token. Else, the token can expire during long-running deployments.
            Func<IResourceManagementClient> createArmClient = () => new ResourceManagementClient(new TokenCloudCredentials(subscriptionId, ServicePrincipal.GetAuthorizationToken(tenantId, clientId, password)));

            CreateDeployment(createArmClient, resourceGroupName, deploymentName, deploymentMode, template, parameters);
            PollForCompletion(createArmClient, resourceGroupName, deploymentName, variables);
        }

        static string GenerateDeploymentNameFromStepName(string stepName)
        {
            var deploymentName = stepName ?? string.Empty;
            deploymentName = deploymentName.ToLower();
            deploymentName = Regex.Replace(deploymentName, "\\s", "-");
            deploymentName =
                new string(deploymentName.Select(x => (char.IsLetterOrDigit(x) || x == '-') ? x : '-').ToArray());
            deploymentName = Regex.Replace(deploymentName, "-+", "-");
            deploymentName = deploymentName.Trim('-', '/');
            deploymentName = deploymentName + "-" + AesEncryption.RandomString(6);
            return deploymentName;
        }

        static void CreateDeployment(Func<IResourceManagementClient> createArmClient, string resourceGroupName, string deploymentName,
            DeploymentMode deploymentMode, string template, IDictionary<string, ResourceGroupTemplateParameter> parameters)
        {
            var parameterJson = parameters != null ? JsonConvert.SerializeObject(parameters, Formatting.Indented) : null;

            Log.Verbose($"Template:\n{template}\n");
            if (parameterJson != null)
            {
               Log.Verbose($"Parameters:\n{parameterJson}\n"); 
            }

            using (var armClient = createArmClient())
            {
                var createDeploymentResult = armClient.Deployments.CreateOrUpdate(resourceGroupName, deploymentName,
                    new Microsoft.Azure.Management.Resources.Models.Deployment
                    {
                        Properties = new DeploymentProperties
                        {
                            Mode = deploymentMode,
                            Template = template,
                            Parameters = parameterJson
                        }
                    });

                Log.Info($"Deployment created: {createDeploymentResult.Deployment.Id}");
            }
        }

        static void PollForCompletion(Func<IResourceManagementClient> createArmClient, string resourceGroupName,
            string deploymentName, VariableDictionary variables)
        {
            // While the deployment is running, we poll to check it's state.
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
                    var deployment = armClient.Deployments.Get(resourceGroupName, deploymentName).Deployment;

                    Log.Verbose($"Provisioning state: {deployment.Properties.ProvisioningState}");

                    switch (deployment.Properties.ProvisioningState)
                    {
                        case "Succeeded":
                            Log.Info($"Deployment {deploymentName} complete.");
                            Log.Info(GetOperationResults(armClient, resourceGroupName, deploymentName));
                            CaptureOutputs(deployment.Properties.Outputs, variables);
                            continueToPoll = false;
                            break;

                        case "Failed":
                            throw new CommandException($"Azure Resource Group deployment {deploymentName} failed:\n" +
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
            var log = new StringBuilder("Operations details:\n");
            var operations =
                armClient.DeploymentOperations.List(resourceGroupName, deploymentName, new DeploymentOperationsListParameters()).Operations;

            foreach (var operation in operations)
            {
                log.AppendLine($"Resource: {operation.Properties.TargetResource.ResourceName}");
                log.AppendLine($"Type: {operation.Properties.TargetResource.ResourceType}");
                log.AppendLine($"Timestamp: {operation.Properties.Timestamp.ToLocalTime().ToString("s")}");
                log.AppendLine($"Deployment operation: {operation.Id}");
                log.AppendLine($"Status: {operation.Properties.StatusCode}");
                log.AppendLine($"Provisioning State: {operation.Properties.ProvisioningState}");
                if (operation.Properties.StatusMessage != null)
                {
                    log.AppendLine($"Status Message: {JsonConvert.SerializeObject(operation.Properties.StatusMessage)}");
                }
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