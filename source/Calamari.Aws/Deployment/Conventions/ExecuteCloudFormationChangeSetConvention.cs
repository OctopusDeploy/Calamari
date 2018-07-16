
using System;
using System.Diagnostics;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Octopus.CoreUtilities;

namespace Calamari.Aws.Deployment.Conventions
{
    public class ExecuteCloudFormationChangeSetConvention : IInstallConvention
    {
        private readonly Func<AmazonCloudFormationClient> clientFactory;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        private readonly Func<RunningDeployment, ChangeSetArn> changeSetProvider;
        private readonly StackEventLogger logger;
        private readonly bool waitForComplete;

        public ExecuteCloudFormationChangeSetConvention(
            Func<AmazonCloudFormationClient> clientFactory,
            Func<RunningDeployment, StackArn> stackProvider, 
            Func<RunningDeployment, ChangeSetArn> changeSetProvider,
            StackEventLogger logger,
            bool waitForComplete)
        {
            Guard.NotNull(stackProvider, "Stack provider must not be null");
            Guard.NotNull(changeSetProvider, "Change set provider must nobe null");
            Guard.NotNull(clientFactory, "Client factory should not be null");

            this.clientFactory = clientFactory;
            this.stackProvider = stackProvider;
            this.changeSetProvider = changeSetProvider;
            this.logger = logger;
            this.waitForComplete = waitForComplete;
        }

        public void Install(RunningDeployment deployment)
        {
            var stack = stackProvider(deployment);
            var changeSet = changeSetProvider(deployment);

            Guard.NotNull(stack, "The provided stack identifer or name may not be null");
            Guard.NotNull(changeSet, "The provided change set identifier or name may not be null");

            var result = ExecuteChangeset(clientFactory, stack, changeSet);

            if (result.None())
            {
                Log.Info("No changes changes are to be performed.");
                return;
            }

            if (waitForComplete)
            {
                clientFactory.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stack, status =>
                {
                    logger.Log(status);
                    logger.LogRollbackError(status, x => clientFactory.GetLastStackEvent(stack, x));
                    return status.ToMaybe().SelectValueOr(x =>
                        (x.ResourceStatus.Value.EndsWith("_COMPLETE") ||
                         x.ResourceStatus.Value.EndsWith("_FAILED")) &&
                        x.ResourceType.Equals("AWS::CloudFormation::Stack"), true);
                });
            }
        }

        private Maybe<RunningChangeSet> ExecuteChangeset(Func<AmazonCloudFormationClient> factory, StackArn stack,
            ChangeSetArn changeSet)
        {
            try
            {
                var changes = factory.WaitForChangeSetCompletion(CloudFormationDefaults.StatusWaitPeriod, new RunningChangeSet(stack, changeSet));

                if (changes.Status == ChangeSetStatus.FAILED &&
                    string.Compare(changes.StatusReason, "No updates are to be performed.",
                        StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    //We don't need the failed changeset to hang around if there are no changes
                    factory().DeleteChangeSet(new DeleteChangeSetRequest
                    {
                        ChangeSetName = changeSet.Value,
                        StackName = stack.Value
                    });
                    
                    return Maybe<RunningChangeSet>.None;
                }

                factory().ExecuteChangeSet(new ExecuteChangeSetRequest
                {
                    ChangeSetName = changeSet.Value,
                    StackName = stack.Value
                });

                return new RunningChangeSet(stack, changeSet).AsSome();
            }
            catch (AmazonCloudFormationException exception)
            {
                if (exception.ErrorCode == "AccessDenied")
                {
                    throw new PermissionException(
                        "AWS-CLOUDFORMATION-ERROR-0011: The AWS account used to perform the operation does not have " +
                        "the required permissions to update the stack.\n" +
                        exception.Message + "\n" +
                        "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0011");
                }

                throw new UnknownException(
                    "AWS-CLOUDFORMATION-ERROR-0011: An unrecognised exception was thrown while updating a CloudFormation stack.\n" +
                    "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0011",
                    exception);
            }
        }
    }

    public class VariableOutput
    {
        public VariableOutput(string name, string value)
        {
            Name = name;
            Value = value;
        }
        
        public string Name { get; }
        public string Value { get; }
    }
}