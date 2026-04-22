using System;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Deployment.Conventions
{
    // SPF parity: fail fast (do not wait) if the stack is currently in an IN_PROGRESS state.
    // See step-package-ecs/steps/ecs-deploy-service-v2/src/cloudformation/cloudFormationExecutor.ts.
    public class CheckEcsStackNotInProgressConvention : IInstallConvention
    {
        static readonly string[] InProgressStates =
        {
            "CREATE_IN_PROGRESS", "UPDATE_IN_PROGRESS", "DELETE_IN_PROGRESS",
            "IMPORT_IN_PROGRESS", "REVIEW_IN_PROGRESS", "IMPORT_ROLLBACK_IN_PROGRESS",
            "ROLLBACK_IN_PROGRESS", "UPDATE_COMPLETE_CLEANUP_IN_PROGRESS",
            "UPDATE_ROLLBACK_IN_PROGRESS", "UPDATE_ROLLBACK_COMPLETE_CLEANUP_IN_PROGRESS"
        };

        readonly AwsEnvironmentGeneration environment;
        readonly string stackName;
        readonly ILog log;

        public CheckEcsStackNotInProgressConvention(AwsEnvironmentGeneration environment, string stackName, ILog log)
        {
            this.environment = environment;
            this.stackName = stackName;
            this.log = log;
        }

        public void Install(RunningDeployment deployment) =>
            InstallAsync().GetAwaiter().GetResult();

        async Task InstallAsync()
        {
            using var client = ClientHelpers.CreateCloudFormationClient(environment);
            var stack = await DescribeStack(client);
            if (stack != null && InProgressStates.Contains(stack.StackStatus?.Value))
            {
                throw new CommandException(
                    $"[ECS-Deployment-StackIsInInProgressState](https://g.octopushq.com/ECS-Deployment-StackIsInInProgressState) " +
                    $"Unable to deploy. The CloudFormation stack named \"{stackName}\" is in an \"IN_PROGRESS\" state. Please try again later.");
            }
        }

        async Task<Stack> DescribeStack(IAmazonCloudFormation client)
        {
            try
            {
                var response = await client.DescribeStacksAsync(new DescribeStacksRequest { StackName = stackName });
                return response.Stacks?.FirstOrDefault(s => s.StackName == stackName);
            }
            catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "ValidationError")
            {
                return null;
            }
        }
    }
}
