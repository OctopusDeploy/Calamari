using System;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class DeleteCloudFormationStackConvention : CloudFormationInstallationConventionBase
    {
        private readonly Func<IAmazonCloudFormation> clientFactory;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        private readonly AwsEnvironmentGeneration environment;
        private readonly bool waitForComplete;

        public DeleteCloudFormationStackConvention(
            AwsEnvironmentGeneration environment,
            StackEventLogger logger,
            Func<IAmazonCloudFormation> clientFactory,
            Func<RunningDeployment, StackArn> stackProvider,
            bool waitForComplete
        ): base(logger)
        {
            Guard.NotNull(clientFactory, "Client must not be null");
            Guard.NotNull(stackProvider, "Stack provider must not be null");
            Guard.NotNull(environment, "Aws environment generation may not be null");
            
            
            this.clientFactory = clientFactory;
            this.stackProvider = stackProvider;
            this.waitForComplete = waitForComplete;
            this.environment = environment;
        }
        
        public override void Install(RunningDeployment deployment)
        {
            InstallAsync(deployment).GetAwaiter().GetResult();
        }

        private async Task InstallAsync(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            var deploymentStartTime = DateTime.Now;

            var stack = stackProvider(deployment);

            if (await clientFactory.StackExistsAsync(stack, StackStatus.Completed) != StackStatus.DoesNotExist)
            {
                await DeleteCloudFormation(stack);
                Log.Info($"Deleted stack called {stack.Value} in region {environment.AwsRegion.SystemName}");
            }
            else
            {
                Log.Info($"No stack called {stack.Value} exists in region {environment.AwsRegion.SystemName}");
                return;
            }

            if (waitForComplete)
            {
                await WithAmazonServiceExceptionHandling(async () =>
                {
                    await clientFactory.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stack,
                        LogAndThrowRollbacks(clientFactory, stack, true, false, FilterStackEventsSince(deploymentStartTime)));
                });
            }
        }

        private Task DeleteCloudFormation(StackArn stack)
        {
            Guard.NotNull(stack, "Stack must not be null");
            return WithAmazonServiceExceptionHandling(async () =>
            {
                await clientFactory.DeleteStackAsync(stack);
            });
        }
    }
}