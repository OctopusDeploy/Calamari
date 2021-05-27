using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment;
using Calamari.Util;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    /// <summary>
    /// Describes the state of the stack
    /// </summary>
    public enum StackStatus
    {
        DoesNotExist,
        Completed,
        InProgress
    }

    public class DeployAwsCloudFormationConvention : CloudFormationInstallationConventionBase
    {

        private readonly Func<IAmazonCloudFormation> clientFactory;
        private readonly Func<CloudFormationTemplate> templateFactory;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        private readonly Func<RunningDeployment, string> roleArnProvider;
        private readonly bool waitForComplete;
        private readonly string stackName;
        private readonly bool disableRollback;
        private readonly List<string> capabilities = new List<string>();
        private readonly AwsEnvironmentGeneration awsEnvironmentGeneration;
        // This must allow nulls to distinguish between no tags specified - which preserves existing CF tags,
        // and an empty list specified - which clears out all existing tags.
        private readonly List<Tag> tags;

        public DeployAwsCloudFormationConvention(
            Func<IAmazonCloudFormation> clientFactory,
            Func<CloudFormationTemplate> templateFactory,
            StackEventLogger logger,
            Func<RunningDeployment, StackArn> stackProvider,
            Func<RunningDeployment, string> roleArnProvider,
            bool waitForComplete,
            string stackName,
            IEnumerable<string> iamCapabilities,
            bool disableRollback,
            AwsEnvironmentGeneration awsEnvironmentGeneration,
            IEnumerable<KeyValuePair<string, string>> tags = null): base(logger)
        {
            this.clientFactory = clientFactory;
            this.templateFactory = templateFactory;
            this.stackProvider = stackProvider;
            this.roleArnProvider = roleArnProvider;
            this.waitForComplete = waitForComplete;
            this.stackName = stackName;
            this.awsEnvironmentGeneration = awsEnvironmentGeneration;
            this.disableRollback = disableRollback;
            this.tags = tags?.Select(x => new Tag { Key = x.Key, Value = x.Value }).ToList();

            // https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/using-iam-template.html#capabilities
            var (validCapabilities, _) = ExcludeAndLogUnknownIamCapabilities(iamCapabilities);
            capabilities.AddRange(validCapabilities);
        }

        public override void Install(RunningDeployment deployment)
        {
            InstallAsync(deployment).GetAwaiter().GetResult();
        }

        private async Task InstallAsync(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");
            var stack = stackProvider(deployment);
            Guard.NotNull(stack, "Stack can not be null");
            var template = templateFactory();
            Guard.NotNull(template, "CloudFormation template can not be null.");
            
            await DeployCloudFormation(deployment, stack, template);
        }
        

        private async Task DeployCloudFormation(RunningDeployment deployment, StackArn stack, CloudFormationTemplate template)
        {
            Guard.NotNull(deployment, "deployment can not be null");
           
            await clientFactory.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stack, LogAndThrowRollbacks(clientFactory, stack, false));
            await DeployStack(deployment, stack, template);
        }

        /// <summary>
        /// Update or create the stack
        /// </summary>
        /// <param name="deployment">The current deployment</param>
        /// <param name="stack"></param>
        /// <param name="template"></param>
        private async Task DeployStack(RunningDeployment deployment, StackArn stack, CloudFormationTemplate template)
        {
            Guard.NotNull(deployment, "deployment can not be null");
           
            var stackId = await template.Inputs
                // Use the parameters to either create or update the stack
                .Map(async parameters => await StackExists(stack, StackStatus.DoesNotExist) != StackStatus.DoesNotExist
                    ? await UpdateCloudFormation(deployment, stack, template)
                    : await CreateCloudFormation(deployment, template));

            if (waitForComplete)
            {
                await clientFactory.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stack, LogAndThrowRollbacks(clientFactory, stack));
                await CheckStackDeploymentSuccessAndLogErrors(stack);
            }

            // Take the stack ID returned by the create or update events, and save it as an output variable
            Log.SetOutputVariable("AwsOutputs[StackId]", stackId ?? "", deployment.Variables);
            Log.Info(
                $"Saving variable \"Octopus.Action[{deployment.Variables["Octopus.Action.Name"]}].Output.AwsOutputs[StackId]\"");
        }

        /// <summary>
        /// Checks whether the stack deployment succeeded and logs any errors that occurred during the stack deployment
        /// </summary>
        /// <param name="stack">The stack to check</param>
        private async Task CheckStackDeploymentSuccessAndLogErrors(StackArn stack)
        {
            var lastStackEvent = await StackEvent(stack);
            var isSuccess = lastStackEvent.Select(x => x.MaybeIndicatesSuccess()).SelectValueOr(x => x.Value, true);
            if (!isSuccess)
            {
                var allStackEvents = await clientFactory.GetStackEvents(stack);
                foreach (var @event in allStackEvents)
                {
                    // Only log errors
                    if (!@event.Select(x => x.MaybeIndicatesSuccess()).SelectValueOr(x => x.Value, false))
                        Logger.Warn(@event);
                }
            }
        }

        /// <summary>
        /// Gets the last stack event by timestamp, optionally filtered by a predicate
        /// </summary>
        /// <param name="predicate">The optional predicate used to filter events</param>
        /// <returns>The stack event</returns>
        private Task<Maybe<StackEvent>> StackEvent(StackArn stack, Func<StackEvent, bool> predicate = null)
        {
            return WithAmazonServiceExceptionHandling(
               async () => await clientFactory.GetLastStackEvent(stack, predicate));
        }

        /// <summary>
        /// Check to see if the stack name exists.
        /// </summary>
        /// <param name="defaultValue">The return value when the user does not have the permissions to query the stacks</param>
        /// <returns>The current status of the stack</returns>
        private Task<StackStatus> StackExists(StackArn stack, StackStatus defaultValue)
        {
            return WithAmazonServiceExceptionHandling(() => clientFactory.StackExistsAsync(stack, defaultValue));
        }
        
        /// <summary>
        /// Creates the stack and returns the stack ID
        /// </summary>
        /// <param name="deployment">The running deployment</param>
        /// <returns>The stack id</returns>
        private Task<string> CreateCloudFormation(RunningDeployment deployment, CloudFormationTemplate template)
        {
            Guard.NotNull(template, "template can not be null");

            return WithAmazonServiceExceptionHandling(async () =>
            {
                var stackId = await clientFactory.CreateStackAsync(new CreateStackRequest
                    {
                        StackName = stackName,
                        TemplateBody = template.Content,
                        Parameters = template.Inputs.ToList(),
                        Capabilities = capabilities,
                        DisableRollback = disableRollback,
                        RoleARN = roleArnProvider(deployment),
                        Tags = tags
                    });
                    
                    Log.Info($"Created stack {stackId} in region {awsEnvironmentGeneration.AwsRegion.SystemName}");
                return stackId;
            });
        }

        /// <summary>
        /// Deletes the stack
        /// </summary>
        private Task DeleteCloudFormation(StackArn stack)
        {
            return WithAmazonServiceExceptionHandling(async () =>
            {
                await clientFactory.DeleteStackAsync(stack);
                Log.Info($"Deleted stack called {stackName} in region {awsEnvironmentGeneration.AwsRegion.SystemName}");
            });
        }

        /// <summary>
        /// Updates the stack and returns the stack ID
        /// </summary>
        /// <param name="stack">The stack name or id</param>
        /// <param name="deployment">The current deployment</param>
        /// <param name="template">The CloudFormation template</param>
        /// <returns>stackId</returns>
        private async Task<string> UpdateCloudFormation(
            RunningDeployment deployment,
            StackArn stack,
            CloudFormationTemplate template)
        {
            Guard.NotNull(deployment, "deployment can not be null");
            try
            {
                var result = await ClientHelpers.CreateCloudFormationClient(awsEnvironmentGeneration).UpdateStackAsync(new UpdateStackRequest
                {
                    StackName = stackName,
                    TemplateBody = template.Content,
                    Parameters = template.Inputs.ToList(),
                    Capabilities = capabilities,
                    RoleARN = roleArnProvider(deployment),
                    Tags = tags
                });

                Log.Info(
                    $"Updated stack with id {result.StackId} in region {awsEnvironmentGeneration.AwsRegion.SystemName}");
                            
                return result.StackId;
            }
            catch (AmazonCloudFormationException ex)
            {
                // Some stack states indicate that we can delete the stack and start again. Otherwise we have some other
                // exception that needs to be dealt with.
                if (!(await StackMustBeDeleted(stack)).SelectValueOrDefault(x => x))
                {
                    // Is this an unrecoverable state, or just a stack that has nothing to update?
                    if (DealWithUpdateException(ex))
                    {
                        // There was nothing to update, but we return the id for consistency anyway
                        var  result = await QueryStackAsync(clientFactory, stack);
                         return result.StackId;
                    }
                }

                // If the stack exists, is in a ROLLBACK_COMPLETE state, and was never successfully
                // created in the first place, we can end up here. In this case we try to create
                // the stack from scratch.
                await DeleteCloudFormation(stack);
                await clientFactory.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stack, LogAndThrowRollbacks(clientFactory, stack, false));
                return await CreateCloudFormation(deployment, template);
            }
            catch (AmazonServiceException ex)
            {
                LogAmazonServiceException(ex);
                throw ex;
            }
        }

        /// <summary>
        /// Not all exceptions are bad. Some just mean there is nothing to do, which is fine.
        /// This method will ignore expected exceptions, and rethrow any that are really issues.
        /// </summary>
        /// <param name="ex">The exception we need to deal with</param>
        /// <exception cref="AmazonCloudFormationException">The supplied exception if it really is an error</exception>
        private bool DealWithUpdateException(AmazonCloudFormationException ex)
        {
            Guard.NotNull(ex, "ex can not be null");

            // Unfortunately there are no better fields in the exception to use to determine the
            // kind of error than the message. We are forced to match message strings.
            if (ex.Message.Contains("No updates are to be performed"))
            {
                Log.Info("No updates are to be performed");
                return true;
            }

            if (ex.ErrorCode == "AccessDenied")
            {
                throw new PermissionException(
                    "The AWS account used to perform the operation does not have the required permissions to update the stack.\n" + 
                    "Please ensure the current account has permission to perfrom action 'cloudformation:UpdateStack'.\n" +
                    ex.Message);
            }

            throw new UnknownException("An unrecognised exception was thrown while updating a CloudFormation stack.\n", ex);
        }

        /// <summary>
        /// Check whether the stack must be deleted in order to recover.
        /// </summary>
        /// <param name="stack">The stack id or name</param>
        /// <returns>true if this status indicates that the stack has to be deleted, and false otherwise</returns>
        private async Task<Maybe<bool>> StackMustBeDeleted(StackArn stack)
        {
            try
            {
                return (await StackEvent(stack)).Select(x => x.StackIsUnrecoverable());
            }
            catch (PermissionException)
            {
                // If we can't get the stack status, assume it is not in a state that we can recover from
                return Maybe<bool>.None;
            }
        }
    }
}