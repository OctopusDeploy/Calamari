using System;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class SetEcsOutputVariablesConvention : IInstallConvention
    {
        readonly AwsEnvironmentGeneration environment;
        readonly string stackName;
        readonly string clusterName;
        readonly string taskFamily;
        readonly ILog log;

        public SetEcsOutputVariablesConvention(
            AwsEnvironmentGeneration environment,
            string stackName,
            string clusterName,
            string taskFamily,
            ILog log)
        {
            this.environment = environment;
            this.stackName = stackName;
            this.clusterName = clusterName;
            this.taskFamily = taskFamily;
            this.log = log;
        }

        public void Install(RunningDeployment deployment)
        {
            InstallAsync(deployment).GetAwaiter().GetResult();
        }

        async Task InstallAsync(RunningDeployment deployment)
        {
            var serviceName = await LookupServiceLogicalId();

            SetOutputVariable(deployment.Variables, "ServiceName", serviceName ?? "");
            SetOutputVariable(deployment.Variables, "ClusterName", clusterName);
            SetOutputVariable(deployment.Variables, "CloudFormationStackName", stackName);
            SetOutputVariable(deployment.Variables, "TaskDefinitionFamily", taskFamily);
            SetOutputVariable(deployment.Variables, "Region", environment?.AwsRegion?.SystemName ?? "");
        }

        protected virtual async Task<string> LookupServiceLogicalId()
        {
            // Try to get the service logical resource ID from the CF stack. Best-effort.
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
            catch
            {
                return null;
            }
        }

        void SetOutputVariable(IVariables variables, string name, string value)
        {
            log.SetOutputVariable(name, value ?? "", variables);
            log.Info($"Setting output variable: {name} = {value}");
        }
    }
}
