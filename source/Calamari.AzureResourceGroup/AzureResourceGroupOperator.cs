using System;
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Timeout;

namespace Calamari.AzureResourceGroup
{
    class AzureResourceGroupOperator
    {
        readonly ILog log;

        public AzureResourceGroupOperator(ILog log)
        {
            this.log = log;
        }
        
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

        public async Task PollForCompletion(ArmOperation<ArmDeploymentResource> deploymentOperation, IVariables variables)
        {
            var pollingTimeout = GetPollingTimeout(variables);
            var asyncResourceGroupPollingTimeoutPolicy = Policy.TimeoutAsync<ArmDeploymentResource>(pollingTimeout, TimeoutStrategy.Optimistic);

            log.Info("Polling for deployment completion...");
            try
            {
                var deploymentResult = await asyncResourceGroupPollingTimeoutPolicy.ExecuteAsync(async timeoutCancellationToken =>
                                                                                                 {
                                                                                                     var delayStrategy = DelayStrategy.CreateExponentialDelayStrategy(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
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
                log.SetOutputVariable($"AzureRmOutputs[{output.Key}]", output.Value["value"].ToString(), variables);
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
}