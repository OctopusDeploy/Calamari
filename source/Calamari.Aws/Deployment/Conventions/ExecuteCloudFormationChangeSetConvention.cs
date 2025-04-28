
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
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
            StackEventLogger stackEventLogger,
            Func<RunningDeployment, StackArn> stackProvider, 
            Func<RunningDeployment, ChangeSetArn> changeSetProvider,
            bool waitForComplete,
            ILog log): base(stackEventLogger, log)
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
            InstallAsync(deployment).GetAwaiter().GetResult();
        }
 

        private async Task InstallAsync(RunningDeployment deployment)
        {
            var stack = stackProvider(deployment);
            var changeSet = changeSetProvider(deployment);

            Guard.NotNull(stack, "The provided stack identifer or name may not be null");
            Guard.NotNull(changeSet, "The provided change set identifier or name may not be null");

            var deploymentStartTime = DateTime.Now;
            
            var response = await clientFactory.DescribeChangeSetAsync(stack, changeSet);
            if (response.Changes.Count == 0)
            {
                await clientFactory().DeleteChangeSetAsync(new DeleteChangeSetRequest
                {
                    ChangeSetName = changeSet.Value,
                    StackName = stack.Value
                });

                Log.Info("No changes need to be performed.");
                return;
            }

            var pollPeriod = PollPeriod(deployment);
            await ExecuteChangeset(clientFactory, stack, changeSet, pollPeriod);
            
            if (waitForComplete)
            {
                await WithAmazonServiceExceptionHandling(() =>
                    clientFactory.WaitForStackToComplete(PollPeriod(deployment), stack, LogAndThrowRollbacks(clientFactory, stack, filter: FilterStackEventsSince(deploymentStartTime)))
                );
            }
        }

        async Task<RunningChangeSet> ExecuteChangeset(Func<IAmazonCloudFormation> factory, StackArn stack,
            ChangeSetArn changeSet, TimeSpan pollPeriod)
        {
            try
            {
                var changes = await factory.WaitForChangeSetCompletion(pollPeriod,
                    new RunningChangeSet(stack, changeSet));


                if (changes.Status == ChangeSetStatus.FAILED)
                {
                    throw new UnknownException($"The changeset failed to create.\n{changes.StatusReason}");
                }

                await factory().ExecuteChangeSetAsync(new ExecuteChangeSetRequest
                {
                    ChangeSetName = changeSet.Value,
                    StackName = stack.Value
                });

                return new RunningChangeSet(stack, changeSet);
            }
            catch (AmazonCloudFormationException exception) when (exception.ErrorCode == "AccessDenied")
            {
                throw new PermissionException(
                    "The AWS account used to perform the operation does not have the required permission to execute the changeset.\n" +
                    "Please ensure the current account has permission to perfrom action 'cloudformation:ExecuteChangeSet'.\n" +
                    exception.Message + "\n");
            }
            catch (AmazonServiceException exception)
            {
                LogAmazonServiceException(exception);
                throw;
            }
        }
    }
}