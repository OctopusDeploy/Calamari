using System;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class DeleteCloudFormationStackConvention : IInstallConvention
    {
        private readonly Func<AmazonCloudFormationClient> clientFactory;
        private readonly StackEventLogger logger;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        private readonly IAwsEnvironmentGeneration environment;
        private readonly bool waitForComplete;

        public DeleteCloudFormationStackConvention(
            IAwsEnvironmentGeneration environment,
            StackEventLogger logger,
            Func<AmazonCloudFormationClient> clientFactory,
            Func<RunningDeployment, StackArn> stackProvider,
            bool waitForComplete
        )
        {
            Guard.NotNull(clientFactory, "Client must not be null");
            Guard.NotNull(stackProvider, "Stack provider must not be null");
            Guard.NotNull(environment, "Aws environment generation may not be null");
            
            
            this.clientFactory = clientFactory;
            this.logger = logger;
            this.stackProvider = stackProvider;
            this.waitForComplete = waitForComplete;
            this.environment = environment;
        }
        
        public void Install(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            var stack = stackProvider(deployment);

            if (clientFactory.GetStackStatus(stack, StackStatus.Completed) != StackStatus.DoesNotExist)
            {
                DeleteCloudFormation(stack);
            }
            else
            {
                Log.Info(
                    $"No stack called {stack.Value} exists in region {environment.AwsRegion.SystemName}");
            }

            if (waitForComplete)
            {
                clientFactory.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stack, status =>
                {
                    logger.Log(status);
                    logger.LogRollbackError(status, x => clientFactory.GetLastStackEvent(stack, x), true, false);
                    return status.ToMaybe().SelectValueOr(x => 
                        (x.ResourceStatus.Value.EndsWith("_COMPLETE") ||
                         x.ResourceStatus.Value.EndsWith("_FAILED")) &&
                        x.ResourceType.Equals("AWS::CloudFormation::Stack"), true);
                });
            }
        }

        private void DeleteCloudFormation(StackArn stack)
        {
            try
            {
                clientFactory()
                    // Client becomes the API response
                    .Map(client => client.DeleteStack(new DeleteStackRequest {StackName = stack.Value}))
                    // Log the response details
                    .Tee(status =>
                        Log.Info(
                            $"Deleted stack called {stack.Value} in region {environment.AwsRegion.SystemName}"));
            }
            catch (AmazonCloudFormationException ex)
            {
                if (ex.ErrorCode == "AccessDenied")
                {
                    throw new PermissionException(
                        "AWS-CLOUDFORMATION-ERROR-0009: The AWS account used to perform the operation does not have " +
                        "the required permissions to delete the stack.\n" +
                        ex.Message + "\n" +
                        "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0009");
                }

                throw new UnknownException(
                    "AWS-CLOUDFORMATION-ERROR-0010: An unrecognised exception was thrown while deleting a CloudFormation stack.\n" +
                    "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0010",
                    ex);
            }
            catch (AmazonServiceException ex)
            {
                ex.GetWebExceptionMessage()
                    .Tee(message => logger.Warn("AWS-CLOUDFORMATION-ERROR-0014", message));
                throw ex;
            }
        }
    }
}