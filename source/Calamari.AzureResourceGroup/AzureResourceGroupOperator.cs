using System;
using System.Linq;
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
    static readonly TimeSpan FailureLoggingTimeout = TimeSpan.FromMinutes(2);
    static readonly string[] FailedStates = { "Failed", "Canceled" };
    const int MaxReportedFailures = 10;

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
        await PollForCompletionWithTimeout(deploymentOperation, resourceGroupResource, deploymentName, variables);
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
        await PollForCompletion(deploymentOperation, resourceGroupResource, deploymentName);
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

    async Task PollForCompletionWithTimeout(ArmOperation<ArmDeploymentResource> deploymentOperation,
                                            ResourceGroupResource resourceGroupResource,
                                            string deploymentName,
                                            IVariables variables)
    {
        var pollingTimeout = GetPollingTimeout(variables);
        var asyncResourceGroupPollingTimeoutPolicy = Policy.TimeoutAsync(pollingTimeout, TimeoutStrategy.Optimistic);
        await asyncResourceGroupPollingTimeoutPolicy.ExecuteAsync(ct => Poll(deploymentOperation, resourceGroupResource, deploymentName, ct), CancellationToken.None);
    }

    async Task PollForCompletion(ArmOperation<ArmDeploymentResource> deploymentOperation,
                                 ResourceGroupResource resourceGroupResource,
                                 string deploymentName)
    {
        await Poll(deploymentOperation, resourceGroupResource, deploymentName, CancellationToken.None);
    }

    async Task Poll(ArmOperation<ArmDeploymentResource> deploymentOperation,
                    ResourceGroupResource resourceGroupResource,
                    string deploymentName,
                    CancellationToken cancellationToken)
    {
        log.Info("Polling for deployment completion...");
        try
        {
            var delayStrategy = DelayStrategy.CreateExponentialDelayStrategy(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
            var response = await deploymentOperation.WaitForCompletionAsync(delayStrategy, cancellationToken);
            log.Info($"Deployment completed with status: {response.Value?.Data.Properties?.ProvisioningState}");
        }
        // This Azure exception is thrown for failed deployments. It is handled here to provide specific failure details
        catch (RequestFailedException)
        {
            var failureDetail = await BuildDeploymentFailureMessage(resourceGroupResource, deploymentName);
            if (failureDetail != null)
                log.Error(failureDetail);

            throw;
        }
        catch
        {
            log.Error("Error polling for deployment completion");
            throw;
        }
    }

    async Task<string?> BuildDeploymentFailureMessage(ResourceGroupResource resourceGroupResource,
                                                     string deploymentName)
    {
        try
        {
            using var errorLoggingCancellation = new CancellationTokenSource(FailureLoggingTimeout);

            var deploymentResponse = await resourceGroupResource.GetArmDeploymentAsync(deploymentName, errorLoggingCancellation.Token);
            if (!deploymentResponse.HasValue)
            {
                log.Warn($"Could not retrieve deployment '{deploymentName}' from Azure, so the resources that failed cannot be listed.");
                return null;
            }

            var report = await CollectFailedOperations(deploymentResponse.Value, errorLoggingCancellation.Token);

            if (report.FailureCount == 0)
                return null;

            var sb = new StringBuilder($"Failed Azure resources ({report.FailureCount} of {report.TotalCount} operations failed or were canceled):");
            sb.Append(report.Details);
            if (report.OmittedCount > 0)
                sb.Append($"\n  ... and {report.OmittedCount} more. See the Azure Portal for the full list.");
            sb.Append("\n\nFor full details check Azure Portal > Resource Groups > Deployments, ");
            sb.Append("or see https://aka.ms/arm-deployment-operations for troubleshooting guidance.");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            log.Warn($"Could not retrieve details of the failed Azure resources: {ex.Message}");
            return null;
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

    static async Task<FailedOperationsReport> CollectFailedOperations(ArmDeploymentResource deploymentResource,
                                                                     CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var failureCount = 0;
        var totalCount = 0;

        await foreach (var op in deploymentResource.GetDeploymentOperationsAsync(cancellationToken: cancellationToken))
        {
            totalCount++;

            var properties = op.Properties;
            if (properties == null || !FailedStates.Contains(properties.ProvisioningState ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                continue;

            failureCount++;

            if (failureCount <= MaxReportedFailures)
                sb.Append(FormatFailedOperation(properties));
        }

        return new FailedOperationsReport(sb.ToString(), failureCount, totalCount, Math.Max(0, failureCount - MaxReportedFailures));
    }

    internal static string FormatFailedOperation(ArmDeploymentOperationProperties properties)
    {
        var state = properties.ProvisioningState?.ToUpperInvariant() ?? "FAILED";
        var resourceType = properties.TargetResource?.ResourceType?.ToString();
        var sb = new StringBuilder($"\n  [{state}] {(string.IsNullOrEmpty(resourceType) ? "Unknown Type" : resourceType)} " +
                                   $"'{properties.TargetResource?.ResourceName ?? "Unknown Resource"}'");

        var errorInfo = properties.StatusMessage != null ? ExtractAzureErrorInfo(properties.StatusMessage) : string.Empty;
        if (!string.IsNullOrWhiteSpace(errorInfo))
            sb.Append($"\n     Error: {errorInfo}");

        if (properties.Timestamp.HasValue)
            sb.Append($"\n     Failed at: {properties.Timestamp.Value.UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC");

        return sb.ToString();
    }

    internal static string ExtractAzureErrorInfo(StatusMessage statusMessage)
    {
        var error = statusMessage.Error;
        if (error == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(error.Code) && !string.IsNullOrWhiteSpace(error.Message))
            return $"[{error.Code}] {error.Message}";

        return !string.IsNullOrWhiteSpace(error.Message) ? error.Message : error.Code ?? string.Empty;
    }

    record FailedOperationsReport(string Details, int FailureCount, int TotalCount, int OmittedCount);

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