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
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Timeout;

namespace Calamari.AzureResourceGroup;

class AzureResourceGroupOperator(ILog log) : IAzureResourceGroupOperator
{
    // Used by the ARM-template deploy behaviour: creates the ArmClient and runs the full submit/poll/finalise flow.
    public async Task Deploy(IAzureAccount account,
                             string subscriptionId,
                             string resourceGroupName,
                             string deploymentName,
                             ArmDeploymentMode deploymentMode,
                             string template,
                             string? parameters,
                             IVariables variables)
    {
        var armClient = account.CreateArmClient();
        var resourceGroupResource = armClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));

        log.Info($"Deploying Resource Group {resourceGroupName} in subscription {subscriptionId}.\nDeployment name: {deploymentName}\nDeployment mode: {deploymentMode}");

        var deploymentOperation = await CreateDeployment(resourceGroupResource, deploymentName, deploymentMode, template, parameters);
        await PollForCompletionWithTimeout(deploymentOperation, variables);
        await FinalizeDeployment(deploymentOperation, variables);
    }

    // Used by the Bicep deploy behaviour: creates the resource group first if it does not already exist.
    public async Task DeployCreatingResourceGroup(IAzureAccount account,
                                                  string subscriptionId,
                                                  string resourceGroupName,
                                                  string resourceGroupLocation,
                                                  string deploymentName,
                                                  ArmDeploymentMode deploymentMode,
                                                  string template,
                                                  string? parameters,
                                                  IVariables variables)
    {
        var armClient = account.CreateArmClient();
        var resourceGroupResource = await GetOrCreateResourceGroup(armClient, subscriptionId, resourceGroupName, resourceGroupLocation);

        var deploymentOperation = await CreateDeployment(resourceGroupResource, deploymentName, deploymentMode, template, parameters);
        await PollForCompletion(deploymentOperation);
        await FinalizeDeployment(deploymentOperation, variables);
    }

    async Task<ResourceGroupResource> GetOrCreateResourceGroup(ArmClient armClient, string subscriptionId, string resourceGroupName, string location)
    {
        var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscriptionId));

        var resourceGroups = subscription.GetResourceGroups();
        var existing = await resourceGroups.GetIfExistsAsync(resourceGroupName);

        if (existing.HasValue && existing.Value != null)
            return existing.Value;

        log.Info($"The resource group with the name {resourceGroupName} does not exist");
        log.Info($"Creating resource group {resourceGroupName} in location {location}");

        var resourceGroupData = new ResourceGroupData(location);
        var armOperation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, resourceGroupName, resourceGroupData);
        return armOperation.Value;
    }

    async Task<ArmOperation<ArmDeploymentResource>> CreateDeployment(ResourceGroupResource resourceGroupResource,
                                                                            string deploymentName,
                                                                            ArmDeploymentMode deploymentMode,
                                                                            string template,
                                                                            string? parameters)
    {
        log.Verbose($"Template:\n{template}\n");
        if (parameters != null)
            log.Verbose($"Parameters:\n{parameters}\n");

        try
        {
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

    async Task PollForCompletionWithTimeout(ArmOperation<ArmDeploymentResource> deploymentOperation, IVariables variables)
    {
        var pollingTimeout = GetPollingTimeout(variables);
        var asyncResourceGroupPollingTimeoutPolicy = Policy.TimeoutAsync(pollingTimeout, TimeoutStrategy.Optimistic);
        await asyncResourceGroupPollingTimeoutPolicy.ExecuteAsync(ct => Poll(deploymentOperation, ct), CancellationToken.None);
    }

    async Task PollForCompletion(ArmOperation<ArmDeploymentResource> deploymentOperation)
    {
        await Poll(deploymentOperation, CancellationToken.None);
    }

    async Task Poll(ArmOperation<ArmDeploymentResource> deploymentOperation, CancellationToken cancellationToken)
    {
        log.Info("Polling for deployment completion...");
        try
        {
            var delayStrategy = DelayStrategy.CreateExponentialDelayStrategy(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
            var response = await deploymentOperation.WaitForCompletionAsync(delayStrategy, cancellationToken);
            log.Info($"Deployment completed with status: {response.Value?.Data.Properties?.ProvisioningState}");
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
        CaptureOutputs(operation.Value.Data.Properties.Outputs?.ToString(), variables);
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

    void CaptureOutputs(string? outputsJson, IVariables variables)
    {
        if (string.IsNullOrWhiteSpace(outputsJson))
            return;

        log.Verbose("Deployment Outputs:");
        log.Verbose(outputsJson);

        var outputs = JObject.Parse(outputsJson);
        foreach (var output in outputs)
        {
            if (output.Value?["value"] is not null)
            {
                log.SetOutputVariable($"AzureRmOutputs[{output.Key}]", output.Value["value"]!.ToString(), variables);
            }
        }
    }

    static TimeSpan GetPollingTimeout(IVariables variables)
    {
        var pollingTimeoutVariableValue = variables.GetInt32(SpecialVariables.Action.Azure.ArmDeploymentTimeout);
        if (pollingTimeoutVariableValue.HasValue && pollingTimeoutVariableValue.Value > 0)
        {
            return TimeSpan.FromMinutes(pollingTimeoutVariableValue.Value);
        }
        return TimeSpan.FromMinutes(30);
    }
}