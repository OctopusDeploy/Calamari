using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Timeout;

namespace Calamari.AzureResourceGroup;

class AzureResourceGroupOperator(ILog log)
{
    public async Task<ArmOperation<ArmDeploymentResource>> CreateDeployment(ResourceGroupResource resourceGroupResource,
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

    public async Task PollForCompletionWithTimeout(ArmOperation<ArmDeploymentResource> deploymentOperation,
                                                   ResourceGroupResource resourceGroupResource,
                                                   string deploymentName,
                                                   IVariables variables)
    {
        var pollingTimeout = GetPollingTimeout(variables);
        var asyncResourceGroupPollingTimeoutPolicy = Policy.TimeoutAsync(pollingTimeout, TimeoutStrategy.Optimistic);
        await asyncResourceGroupPollingTimeoutPolicy.ExecuteAsync(ct => Poll(deploymentOperation, resourceGroupResource, deploymentName, ct), CancellationToken.None);
    }

    public async Task PollForCompletion(ArmOperation<ArmDeploymentResource> deploymentOperation,
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
        catch (RequestFailedException ex)
        {
            var enhancedMessage = await TryEnhanceDeploymentError(resourceGroupResource, deploymentName, ex);
            log.Error(enhancedMessage);
            throw;
        }
        catch (Exception ex)
        {
            log.Error($"Error polling for deployment completion: {ex.Message}");
            throw;
        }
    }

    public async Task FinalizeDeployment(ArmOperation<ArmDeploymentResource> operation, IVariables variables)
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
                sb.AppendLine($"Status Message: {FormatStatusMessage(properties.StatusMessage)}");
            sb.Append(" \n");
        }

        log.Info(sb.ToString());
    }

    async Task<string> TryEnhanceDeploymentError(ResourceGroupResource resourceGroupResource,
                                                  string deploymentName,
                                                  RequestFailedException originalException)
    {
        try
        {
            log.Verbose($"Attempting to retrieve detailed operation information for failed deployment '{deploymentName}'...");

            ArmDeploymentResource? deploymentResource = null;
            try
            {
                var deploymentResponse = await resourceGroupResource.GetArmDeploymentAsync(deploymentName);
                if (deploymentResponse.HasValue)
                    deploymentResource = deploymentResponse.Value;
            }
            catch (Exception ex)
            {
                log.Verbose($"Could not retrieve deployment resource for error detail: {ex.Message}");
            }

            if (deploymentResource == null)
                return $"Error polling for deployment completion: {originalException.Message}";

            var operations = new List<string>();
            var failureCount = 0;
            var totalOperations = 0;

            await foreach (var op in deploymentResource.GetDeploymentOperationsAsync())
            {
                totalOperations++;
                var properties = op.Properties;

                if (properties?.ProvisioningState == "Failed")
                {
                    failureCount++;
                    var resourceName = properties.TargetResource?.ResourceName ?? "Unknown Resource";
                    var resourceType = properties.TargetResource?.ResourceType ?? "Unknown Type";

                    var failureDetail = $"\n  [FAILED] {resourceType} '{resourceName}'";

                    if (properties.StatusMessage != null)
                    {
                        var errorInfo = ExtractAzureErrorInfo(properties.StatusMessage);
                        if (!string.IsNullOrWhiteSpace(errorInfo))
                            failureDetail += $"\n     Error: {errorInfo}";
                    }

                    if (properties.Timestamp.HasValue)
                        failureDetail += $"\n     Failed at: {properties.Timestamp.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

                    operations.Add(failureDetail);
                }
            }

            log.Verbose($"Found {totalOperations} total operations, {failureCount} failed");

            if (operations.Any())
            {
                return $"Error polling for deployment completion: {originalException.Message}\n\n" +
                       $"FAILED AZURE RESOURCES ({failureCount} of {totalOperations} operations failed):" +
                       string.Join("", operations) +
                       "\n\nFor full details check Azure Portal > Resource Groups > Deployments, " +
                       "or see https://aka.ms/arm-deployment-operations for troubleshooting guidance.";
            }

            if (totalOperations > 0)
            {
                return $"Error polling for deployment completion: {originalException.Message}\n\n" +
                       $"Found {totalOperations} deployment operations but none were marked as failed. " +
                       "Check the Azure Portal for detailed deployment status.";
            }

            return $"Error polling for deployment completion: {originalException.Message}";
        }
        catch (Exception enhancementEx)
        {
            log.Verbose($"Failed to retrieve detailed deployment error information: {enhancementEx.Message}");
            return $"Error polling for deployment completion: {originalException.Message}";
        }
    }

    static string ExtractAzureErrorInfo(StatusMessage statusMessage)
    {
        var error = statusMessage.Error;
        if (error == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(error.Code) && !string.IsNullOrWhiteSpace(error.Message))
            return $"[{error.Code}] {error.Message}";
        if (!string.IsNullOrWhiteSpace(error.Message))
            return error.Message;
        if (!string.IsNullOrWhiteSpace(error.Code))
            return error.Code;

        return string.Empty;
    }

    static string FormatStatusMessage(StatusMessage statusMessage)
    {
        var errorInfo = ExtractAzureErrorInfo(statusMessage);
        if (!string.IsNullOrWhiteSpace(errorInfo))
            return errorInfo;

        // Fall back to JSON for status messages without a typed error (e.g. success responses)
        try
        {
            var json = JObject.FromObject(statusMessage);
            return json.ToString();
        }
        catch
        {
            return statusMessage.ToString() ?? string.Empty;
        }
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