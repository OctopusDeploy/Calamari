using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Aws.Util;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Util;
using Newtonsoft.Json;
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

    public class DeployAwsCloudFormationConvention : IInstallConvention
    {
        /// <summary>
        /// These are the capabilities that we recognise. All others are ignored.
        /// </summary>
        private static readonly string[] RecognisedCapabilities = new[] {"CAPABILITY_IAM", "CAPABILITY_NAMED_IAM"};

        private readonly Func<AmazonCloudFormationClient> clientFactory;
        private readonly CloudFormationTemplate template;
        private readonly StackEventLogger logger;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        private readonly bool waitForComplete;
        private readonly string stackName;
        private readonly bool disableRollback;
        private readonly List<string> capabilities = new List<string>();
        private readonly IAwsEnvironmentGeneration awsEnvironmentGeneration;

        public DeployAwsCloudFormationConvention(
            Func<AmazonCloudFormationClient> clientFactory,
            CloudFormationTemplate template,
            StackEventLogger logger,
            Func<RunningDeployment, StackArn> stackProvider,
            bool waitForComplete,
            string stackName,
            string iamCapabilities,
            bool disableRollback,
            IAwsEnvironmentGeneration awsEnvironmentGeneration)
        {
            this.clientFactory = clientFactory;
            this.template = template;
            this.logger = logger;
            this.stackProvider = stackProvider;
            this.waitForComplete = waitForComplete;
            this.stackName = stackName;
            this.awsEnvironmentGeneration = awsEnvironmentGeneration;
            this.disableRollback = disableRollback;

            // https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/using-iam-template.html#capabilities
            if (RecognisedCapabilities.Contains(iamCapabilities))
            {
                capabilities.Add(iamCapabilities);
            }
        }

        public void Install(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");
            var stack = stackProvider(deployment);
            
            Guard.NotNull(stack, "Stack can not be null");
            DeployCloudFormation(deployment, stack);
        }

        private void DeployCloudFormation(RunningDeployment deployment, StackArn stack)
        {
            Guard.NotNull(deployment, "deployment can not be null");
            clientFactory.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stack, StackEventCompleted(stack, false));
            DeployStack(deployment, stack);
        }

        private Func<Maybe<StackEvent>, bool> StackEventCompleted(StackArn stack, bool expectSuccess = true, bool missingIsFailure = true)
        {
            return @event =>
            {
                try
                {
                    logger.Log(@event);
                    logger.LogRollbackError(@event, x => StackEvent(stack, x), expectSuccess,
                        missingIsFailure);
                    return @event.SelectValueOr(x =>
                        (x.ResourceStatus.Value.EndsWith("_COMPLETE") ||
                         x.ResourceStatus.Value.EndsWith("_FAILED")) &&
                        x.ResourceType.Equals("AWS::CloudFormation::Stack"), true);
                }
                catch (PermissionException exception)
                {
                    Log.Warn(exception.Message);
                    return true;
                }

            };
        }


        /// <summary>
        /// Update or create the stack
        /// </summary>
        /// <param name="deployment">The current deployment</param>
        /// <param name="stack"></param>
        private void DeployStack(RunningDeployment deployment, StackArn stack)
        {
            Guard.NotNull(deployment, "deployment can not be null");
           
            var stackId = template.Inputs
                // Use the parameters to either create or update the stack
                .Map(parameters => StackExists(stack, StackStatus.DoesNotExist) != StackStatus.DoesNotExist
                    ? UpdateCloudFormation(deployment, stack)
                    : CreateCloudFormation(deployment));

            if (waitForComplete)
            {
                clientFactory.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stack, StackEventCompleted(stack));
            }

            // Take the stack ID returned by the create or update events, and save it as an output variable
            Log.SetOutputVariable("AwsOutputs[StackId]", stackId ?? "", deployment.Variables);
            Log.Info(
                $"Saving variable \"Octopus.Action[{deployment.Variables["Octopus.Action.Name"]}].Output.AwsOutputs[StackId]\"");
        }


        /// <summary>
        /// Gets the last stack event by timestamp, optionally filtered by a predicate
        /// </summary>
        /// <param name="predicate">The optional predicate used to filter events</param>
        /// <returns>The stack event</returns>
        private Maybe<StackEvent> StackEvent(StackArn stack, Func<StackEvent, bool> predicate = null)
        {
            return WithAmazonServiceExceptionHandling(() => clientFactory.GetLastStackEvent(stack, predicate));
        }

        /// <summary>
        /// Check to see if the stack name exists.
        /// </summary>
        /// <param name="defaultValue">The return value when the user does not have the permissions to query the stacks</param>
        /// <returns>The current status of the stack</returns>
        private StackStatus StackExists(StackArn stack, StackStatus defaultValue)
        {
            return WithAmazonServiceExceptionHandling(() => clientFactory.GetStackStatus(stack, defaultValue));
        }
        
        /// <summary>
        /// Creates the stack and returns the stack ID
        /// </summary>
        /// <param name="deployment">The running deployment</param>
        /// <returns>The stack id</returns>
        private string CreateCloudFormation(RunningDeployment deployment)
        {
            Guard.NotNull(template, "template can not be null");

            try
            {
                return clientFactory.CreateStack(new CreateStackRequest
                {
                    StackName = stackName,
                    TemplateBody = template.ApplyVariableSubstitution(deployment.Variables),
                    Parameters = template.Inputs.ToList(),
                    Capabilities = capabilities,
                    DisableRollback = disableRollback
                })
                .Tee(stackId => Log.Info($"Created stack with id {stackId} in region {awsEnvironmentGeneration.AwsRegion.SystemName}"));
            }
            catch (AmazonServiceException ex)
            {
                HandleAmazonServiceException(ex);
                throw;
            }
        }

        /// <summary>
        /// Deletes the stack
        /// </summary>
        private void DeleteCloudFormation(StackArn stack)
        {
            try
            {
                clientFactory.DeleteStack(stack);
                Log.Info($"Deleted stack called {stackName} in region {awsEnvironmentGeneration.AwsRegion.SystemName}");
            }
            catch (AmazonServiceException ex)
            {
                HandleAmazonServiceException(ex);
                throw;
            }
        }
        
        /// <summary>
        /// Updates the stack and returns the stack ID
        /// </summary>
        /// <param name="stack">The stack name or id</param>
        /// <param name="deployment">The current deployment</param>
        /// <returns>stackId</returns>
        private string UpdateCloudFormation(
            RunningDeployment deployment,
            StackArn stack)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            try
            {
                return ClientHelpers.CreateCloudFormationClient(awsEnvironmentGeneration)
                    // Client becomes the API response
                    .Map(client => client.UpdateStack(
                        new UpdateStackRequest
                        {
                            StackName = stackName,
                            TemplateBody = template.ApplyVariableSubstitution(deployment.Variables),
                            Parameters = template.Inputs.ToList(),
                            Capabilities = capabilities
                        }))
                    // Narrow to the stack id
                    .Map(response => response.StackId)
                    // Log the stack id
                    .Tee(stackId =>
                        Log.Info(
                            $"Updated stack with id {stackId} in region {awsEnvironmentGeneration.AwsRegion.SystemName}"));
            }
            catch (AmazonCloudFormationException ex)
            {
                // Some stack states indicate that we can delete the stack and start again. Otherwise we have some other
                // exception that needs to be dealt with.
                if (!StackMustBeDeleted(stack, false))
                {
                    // Is this an unrecoverable state, or just a stack that has nothing to update?
                    if (DealWithUpdateException(ex))
                    {
                        // There was nothing to update, but we return the id for consistency anyway
                        return clientFactory.DescribeStack(stack).StackId;
                    }
                }

                // If the stack exists, is in a ROLLBACK_COMPLETE state, and was never successfully
                // created in the first place, we can end up here. In this case we try to create
                // the stack from scratch.
                DeleteCloudFormation(stack);
                clientFactory.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stack, StackEventCompleted(stack, false));
                return CreateCloudFormation(deployment);
            }
            catch (AmazonServiceException ex)
            {
                HandleAmazonServiceException(ex);
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
                    "AWS-CLOUDFORMATION-ERROR-0011: The AWS account used to perform the operation does not have " +
                    "the required permissions to update the stack.\n" +
                    ex.Message + "\n" +
                    "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0011");
            }

            throw new UnknownException(
                "AWS-CLOUDFORMATION-ERROR-0011: An unrecognised exception was thrown while updating a CloudFormation stack.\n" +
                "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0011",
                ex);
        }

        /// <summary>
        /// Some statuses indicate that the only way forward is to delete the stack and try again.
        /// Here are some of the explainations of the stack states from the docs.
        /// 
        /// CREATE_FAILED: Unsuccessful creation of one or more stacks. View the stack events to see any associated error
        /// messages. Possible reasons for a failed creation include insufficient permissions to work with all resources
        /// in the stack, parameter values rejected by an AWS service, or a timeout during resource creation. 
        ///  	
        /// DELETE_FAILED: Unsuccessful deletion of one or more stacks. Because the delete failed, you might have some
        /// resources that are still running; however, you cannot work with or update the stack. Delete the stack again
        /// or view the stack events to see any associated error messages. 
        /// 
        /// ROLLBACK_COMPLETE: This status exists only after a failed stack creation. It signifies that all operations
        /// from the partially created stack have been appropriately cleaned up. When in this state, only a delete operation
        /// can be performed.
        /// 
        /// ROLLBACK_FAILED: Unsuccessful removal of one or more stacks after a failed stack creation or after an explicitly
        /// canceled stack creation. Delete the stack or view the stack events to see any associated error messages.
        /// 
        /// UPDATE_ROLLBACK_FAILED: Unsuccessful return of one or more stacks to a previous working state after a failed stack
        /// update. When in this state, you can delete the stack or continue rollback. You might need to fix errors before
        /// your stack can return to a working state. Or, you can contact customer support to restore the stack to a usable state. 
        /// 
        /// http://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/using-cfn-describing-stacks.html#w2ab2c15c15c17c11
        /// </summary>
        /// <param name="stack">The stack id or name</param>
        /// <param name="defaultValue">the default value if the status is null</param>
        /// <returns>true if this status indicates that the stack has to be deleted, and false otherwise</returns>
        private bool StackMustBeDeleted(StackArn stack, bool defaultValue)
        {
            try
            {
                return new[]
                    {
                        "CREATE_FAILED", "ROLLBACK_COMPLETE", "ROLLBACK_FAILED", "DELETE_FAILED",
                        "UPDATE_ROLLBACK_FAILED"
                    }
                    .Any(
                        x => StackEvent(stack).SelectValueOr(
                            @event => @event.ResourceStatus.Value.Equals(x, StringComparison.InvariantCultureIgnoreCase), defaultValue));
            }
            catch (PermissionException)
            {
                // If we can't get the stack status, assume it is not in a state that we can recover from
                return false;
            }
        }

        /// <summary>
        /// Display an warning message to the user (without duplicates)
        /// </summary>
        /// <param name="errorCode">The error message code</param>
        /// <param name="message">The error message body</param>
        /// <returns>true if it was displayed, and false otherwise</returns>
        private bool DisplayWarning(string errorCode, string message)
        {
            return logger.Warn(errorCode, message);
        }

        /// <summary>
        /// The AmazonServiceException can hold additional information that is useful to include in
        /// the log.
        /// </summary>
        /// <param name="exception">The exception</param>
        private void HandleAmazonServiceException(AmazonServiceException exception)
        {
            exception.GetWebExceptionMessage()
                .Tee(message => DisplayWarning("AWS-CLOUDFORMATION-ERROR-0014", message));
        }

        /// <summary>
        /// The AmazonServiceException can hold additional information that is useful to include in the log.
        /// </summary>
        /// <param name="func">The exception</param>
        private T WithAmazonServiceExceptionHandling<T>(Func<T> func)
        {
            try
            {
                return func();
            }
            catch (AmazonServiceException exception)
            {
                HandleAmazonServiceException(exception);
                throw;
            }
        }
    }
}