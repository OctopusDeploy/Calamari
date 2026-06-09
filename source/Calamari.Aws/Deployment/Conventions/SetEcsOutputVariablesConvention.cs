using System;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.CloudAccounts.Aws;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Deployment.Conventions;

// Emits output variables matching SPF's ECS step contract.
public class SetEcsOutputVariablesConvention(
    AwsEnvironmentGeneration environment,
    string stackName,
    string clusterName,
    string taskFamily,
    ILog log)
    : IInstallConvention
{
    public void Install(RunningDeployment deployment) => InstallAsync(deployment).GetAwaiter().GetResult();

    async Task InstallAsync(RunningDeployment deployment)
    {
        var serviceName = await LookupServiceLogicalId();

        SetOutputVariable(deployment.Variables, "ServiceName", serviceName ?? string.Empty);
        SetOutputVariable(deployment.Variables, "ClusterName", clusterName);
        SetOutputVariable(deployment.Variables, "CloudFormationStackName", stackName);
        SetOutputVariable(deployment.Variables, "TaskDefinitionFamily", taskFamily);
        SetOutputVariable(deployment.Variables, "Region", environment.AwsRegion.SystemName);
    }

    protected virtual async Task<string> LookupServiceLogicalId()
    {
        try
        {
            using var client = ClientHelpers.CreateCloudFormationClient(environment);
            var response = await client.DescribeStackResourcesAsync(new DescribeStackResourcesRequest
            {
                StackName = stackName
            });

            return response.StackResources
                           ?.FirstOrDefault(r => r.ResourceType == "AWS::ECS::Service")
                           ?.LogicalResourceId;
        }
        catch (Exception ex)
        {
            log.Verbose($"Failed to look up ECS::Service logical ID in stack \"{stackName}\": {ex.Message}");
            return null;
        }
    }

    void SetOutputVariable(IVariables variables, string name, string value)
    {
        log.Info($"Saving variable \"Octopus.Action[{variables["Octopus.Action.Name"]}].Output.{name}\"");
        log.SetOutputVariable(name, value, variables);
    }
}
