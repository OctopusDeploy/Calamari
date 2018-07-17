
using System;
using System.Diagnostics;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Octopus.CoreUtilities;

namespace Calamari.Aws.Deployment.Conventions
{
    public class ExecuteCloudFormationChangeSetConvention : CloudFormationInstallationConventionBase
    {
        private readonly Func<IAmazonCloudFormation> clientFactory;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        private readonly Func<RunningDeployment, ChangeSetArn> changeSetProvider;
        private readonly bool waitForComplete;

        public ExecuteCloudFormationChangeSetConvention(
            Func<IAmazonCloudFormation> clientFactory,
            StackEventLogger logger,
            Func<RunningDeployment, StackArn> stackProvider, 
            Func<RunningDeployment, ChangeSetArn> changeSetProvider,
            bool waitForComplete): base(logger)
        {
            Guard.NotNull(stackProvider, "Stack provider must not be null");
            Guard.NotNull(changeSetProvider, "Change set provider must nobe null");
            Guard.NotNull(clientFactory, "Client factory should not be null");

            this.clientFactory = clientFactory;
            this.stackProvider = stackProvider;
            this.changeSetProvider = changeSetProvider;
            this.waitForComplete = waitForComplete;
        }

        public override void Install(RunningDeployment deployment)
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
                WithAmazonServiceExceptionHandling(() =>
                    clientFactory.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stack, LogAndThrowRollbacks(clientFactory, stack))
                );
            }
        }

        private Maybe<RunningChangeSet> ExecuteChangeset(Func<IAmazonCloudFormation> factory, StackArn stack,
            ChangeSetArn changeSet)
        {
            try
            {
                var changes = factory.WaitForChangeSetCompletion(CloudFormationDefaults.StatusWaitPeriod,
                    new RunningChangeSet(stack, changeSet));

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
            catch (AmazonServiceException exception)
            {
                HandleAmazonServiceException(exception);
                throw;
            }
        }
    }
}