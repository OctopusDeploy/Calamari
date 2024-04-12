using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Calamari.Azure;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octopus.CoreUtilities.Extensions;
using Polly;
using Polly.Timeout;

namespace Calamari.AzureResourceGroup
{
    class DeployAzureResourceGroupBehaviour : IDeployBehaviour
    {
        static readonly TimeSpan PollingTimeout = TimeSpan.FromSeconds(30);

        static readonly TimeoutPolicy<ArmDeploymentResource> AsyncResourceGroupPollingTimeoutPolicy =
            Policy.TimeoutAsync<ArmDeploymentResource>(PollingTimeout, TimeoutStrategy.Optimistic);

        readonly TemplateService templateService;
        readonly IResourceGroupTemplateNormalizer parameterNormalizer;
        readonly ILog log;

        public DeployAzureResourceGroupBehaviour(TemplateService templateService, IResourceGroupTemplateNormalizer parameterNormalizer, ILog log)
        {
            this.templateService = templateService;
            this.parameterNormalizer = parameterNormalizer;
            this.log = log;
        }

        public bool IsEnabled(RunningDeployment context) => FeatureToggle.ModernAzureSdkFeatureToggle.IsEnabled(context.Variables);

        public async Task Execute(RunningDeployment context)
        {
            log.Verbose("Using Modern Azure SDK behaviour.");

            var variables = context.Variables;
            var hasAccessToken = !variables.Get(AccountVariables.Jwt).IsNullOrEmpty();
            var account = hasAccessToken ? (IAzureAccount)new AzureOidcAccount(variables) : new AzureServicePrincipalAccount(variables);

            var armClient = account.CreateArmClient();

            var resourceGroupName = variables.GetRequiredVariable(SpecialVariables.Action.Azure.ResourceGroupName);
            var subscriptionId = variables.GetRequiredVariable(AzureAccountVariables.SubscriptionId);

            var deploymentNameVariable = variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentName];
            var deploymentName = !string.IsNullOrWhiteSpace(deploymentNameVariable)
                ? deploymentNameVariable
                : DeploymentName.FromStepName(variables[ActionVariables.Name]);
            
            var deploymentModeVariable = variables.GetRequiredVariable(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode);
            var deploymentMode = (ArmDeploymentMode)Enum.Parse(typeof(ArmDeploymentMode), deploymentModeVariable);

            var templateFile = variables.Get(SpecialVariables.Action.Azure.Template, "template.json");
            var templateParametersFile = variables.Get(SpecialVariables.Action.Azure.TemplateParameters, "parameters.json");
            var templateSource = variables.Get(SpecialVariables.Action.Azure.TemplateSource, String.Empty);

            var filesInPackageOrRepository = templateSource == "Package" || templateSource == "GitRepository";
            if (filesInPackageOrRepository)
            {
                templateFile = variables.Get(SpecialVariables.Action.Azure.ResourceGroupTemplate);
                templateParametersFile = variables.Get(SpecialVariables.Action.Azure.ResourceGroupTemplateParameters);
            }

            var template = templateService.GetSubstitutedTemplateContent(templateFile, filesInPackageOrRepository, variables);
            var parameters = !string.IsNullOrWhiteSpace(templateParametersFile)
                ? parameterNormalizer.Normalize(templateService.GetSubstitutedTemplateContent(templateParametersFile, filesInPackageOrRepository, variables))
                : null;

            log.Info($"Deploying Resource Group {resourceGroupName} in subscription {subscriptionId}.\nDeployment name: {deploymentName}\nDeployment mode: {deploymentMode}");

            var deploymentOperation = await CreateDeployment(armClient,
                                                             resourceGroupName,
                                                             subscriptionId,
                                                             deploymentName,
                                                             deploymentMode,
                                                             template,
                                                             parameters);
            await PollForCompletion(deploymentOperation);
            await FinalizeDeployment(deploymentOperation, variables);
        }

        async Task<ArmOperation<ArmDeploymentResource>> CreateDeployment(ArmClient armClient,
                                                                         string resourceGroupName,
                                                                         string subscriptionId,
                                                                         string deploymentName,
                                                                         ArmDeploymentMode deploymentMode,
                                                                         string template,
                                                                         string? parameters)
        {
            log.Verbose($"Template:\n{template}\n");
            if (parameters != null)
            {
                log.Verbose($"Parameters:\n{parameters}\n");
            }

            try
            {
                var resourceGroupResource = armClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));
                var deploymentContent = new ArmDeploymentContent(new ArmDeploymentProperties(deploymentMode)
                {
                    Template = BinaryData.FromString(template),
                    Parameters = parameters != null ? BinaryData.FromString(parameters) : null
                });
                var createDeploymentResult = await resourceGroupResource.GetArmDeployments().CreateOrUpdateAsync(WaitUntil.Started, deploymentName, deploymentContent);

                log.Info($"Deployment {deploymentName} created.");

                return createDeploymentResult;
            }
            catch
            {
                log.Error("Error submitting deployment");
                throw;
            }
        }

        async Task PollForCompletion(ArmOperation<ArmDeploymentResource> deploymentOperation)
        {
            log.Info("Polling for deployment completion...");
            try
            {
                var deploymentResult = await AsyncResourceGroupPollingTimeoutPolicy.ExecuteAsync(async timeoutCancellationToken =>
                                                                                                 {
                                                                                                     var delayStrategy = DelayStrategy.CreateExponentialDelayStrategy(TimeSpan.FromSeconds(1), PollingTimeout);
                                                                                                     var result = await deploymentOperation.WaitForCompletionAsync(delayStrategy, timeoutCancellationToken);
                                                                                                     return result;
                                                                                                 },
                                                                                                 CancellationToken.None);
                log.Info($"Deployment completed with status: {deploymentResult.Data.Properties?.ProvisioningState}");
            }
            catch
            {
                log.Error("Error polling for deployment completion");
                throw;
            }
        }

        async Task FinalizeDeployment(ArmOperation<ArmDeploymentResource> operation, IVariables variables)
        {
            await LogOperationResults(operation);
            CaptureOutputs(operation.Value.Data.Properties.Outputs.ToString(), variables);
        }

        async Task LogOperationResults(ArmOperation<ArmDeploymentResource> operation)
        {
            if (!operation.HasValue || !operation.HasCompleted)
                return;

            var sb = new StringBuilder("Operations details:\n");
            await foreach (var op in operation.Value.GetDeploymentOperationsAsync())
            {
                var properties = op.Properties;
                sb.AppendLine($"Resource: {properties.TargetResource?.ResourceName}");
                sb.AppendLine($"Type: {properties.TargetResource?.ResourceType}");
                sb.AppendLine($"Timestamp: {properties.Timestamp?.ToLocalTime():s}");
                sb.AppendLine($"Deployment operation: {op.Id}");
                sb.AppendLine($"Status: {properties.StatusCode}");
                sb.AppendLine($"Provisioning State: {properties.ProvisioningState}");
                if (properties.StatusMessage != null)
                    sb.AppendLine($"Status Message: {JsonConvert.SerializeObject(properties.StatusMessage)}");
                sb.Append(" \n");
            }

            log.Info(sb.ToString());
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
    }
}