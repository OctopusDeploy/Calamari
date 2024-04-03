using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
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
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
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
            log.Verbose("Using Modern Azure SDK behaviour...");
            
            var variables = context.Variables;
            var hasAccessToken = !variables.Get(AccountVariables.Jwt).IsNullOrEmpty();
            var account = hasAccessToken ? (IAzureAccount)new AzureOidcAccount(variables) : new AzureServicePrincipalAccount(variables);

            var armClient = account.CreateArmClient();
            var resourceGroupName = variables[SpecialVariables.Action.Azure.ResourceGroupName];
            var subscriptionId = variables[AzureAccountVariables.SubscriptionId];

            var deploymentName = !string.IsNullOrWhiteSpace(variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentName])
                ? variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentName]
                : GenerateDeploymentNameFromStepName(variables[ActionVariables.Name]);
            var deploymentMode = (ArmDeploymentMode)Enum.Parse(typeof(ArmDeploymentMode), variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentMode]);

            var templateFile = variables.Get(SpecialVariables.Action.Azure.Template, "template.json");
            var templateParametersFile = variables.Get(SpecialVariables.Action.Azure.TemplateParameters, "parameters.json");
            var filesInPackage = variables.Get(SpecialVariables.Action.Azure.TemplateSource, String.Empty) == "Package";
            if (filesInPackage)
            {
                templateFile = variables.Get(SpecialVariables.Action.Azure.ResourceGroupTemplate);
                templateParametersFile = variables.Get(SpecialVariables.Action.Azure.ResourceGroupTemplateParameters);
            }

            var template = templateService.GetSubstitutedTemplateContent(templateFile, filesInPackage, variables);
            var parameters = !string.IsNullOrWhiteSpace(templateParametersFile)
                ? parameterNormalizer.Normalize(templateService.GetSubstitutedTemplateContent(templateParametersFile, filesInPackage, variables))
                : null;

            // TODO handle null arguments better
            var deploymentOperation = await CreateDeployment(armClient, resourceGroupName, subscriptionId, deploymentName, deploymentMode, template, parameters);
            await PollForCompletion(deploymentOperation);
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
                // log.Info($"Deployment created: {createDeploymentResult.Id}");
                return createDeploymentResult;
            }
            catch (Exception ex) // TODO handle the correct exception
            {
                log.Error("Error submitting deployment");
                log.Error(ex.Message);
                throw;
            }
        }

        async Task PollForCompletion(ArmOperation<ArmDeploymentResource> deploymentOperation)
        {
            log.Verbose("Polling for status of deployment...");
            var deploymentResult = await AsyncResourceGroupPollingTimeoutPolicy.ExecuteAndCaptureAsync(async timeoutCancellationToken =>
            {
                var delayStrategy = DelayStrategy.CreateExponentialDelayStrategy(TimeSpan.FromSeconds(1), PollingTimeout);
                var result = await deploymentOperation.WaitForCompletionAsync(delayStrategy, timeoutCancellationToken);
                return result;
            }, CancellationToken.None);
            
            log.Info($"Deployment completed");
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
    }
}