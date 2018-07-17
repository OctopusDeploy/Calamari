using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Util;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;
using StackStatus = Calamari.Aws.Deployment.Conventions.StackStatus;

namespace Calamari.Aws.Integration.CloudFormation
{
    public static class CloudFormationObjectExtensions
    {
        // These status indicate that an update or create was not successful.
        // http://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/using-cfn-describing-stacks.html#w2ab2c15c15c17c11
        private static HashSet<string> UnsuccessfulStackEvents =
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                "CREATE_ROLLBACK_COMPLETE",
                "CREATE_ROLLBACK_FAILED",
                "UPDATE_ROLLBACK_COMPLETE",
                "UPDATE_ROLLBACK_FAILED",
                "ROLLBACK_COMPLETE",
                "ROLLBACK_FAILED",
                "DELETE_FAILED",
                "CREATE_FAILED"
            };
        
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
        private static HashSet<string> UnrecoverableStackStatuses =
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                "CREATE_FAILED", "ROLLBACK_COMPLETE", "ROLLBACK_FAILED", "DELETE_FAILED",
                "UPDATE_ROLLBACK_FAILED"
            };

        public static DescribeChangeSetResponse DescribeChangeSet(this Func<IAmazonCloudFormation> factory,
            StackArn stack, ChangeSetArn changeSet)
        {
            return factory().Map(client => client.DescribeChangeSet(new DescribeChangeSetRequest
            {
                ChangeSetName = changeSet.Value,
                StackName = stack.Value
            }));
        }

        public static DescribeChangeSetResponse WaitForChangeSetCompletion(
            this Func<IAmazonCloudFormation> clientFactory, TimeSpan waitPeriod, RunningChangeSet runningChangeSet)
        {
            var completion = new HashSet<ChangeSetStatus>
            {
                ChangeSetStatus.FAILED,
                ChangeSetStatus.CREATE_COMPLETE,
                ChangeSetStatus.DELETE_COMPLETE
            };
            while (true)
            {
                var result = clientFactory.DescribeChangeSet(runningChangeSet.Stack, runningChangeSet.ChangeSet);
                if (completion.Contains(result.Status))
                {
                    return result;
                }

                Thread.Sleep(waitPeriod);
            }
        }

        /// <summary>
        /// Gets the last stack event by timestamp, optionally filtered by a predicate
        /// </summary>
        /// <param name="predicate">The optional predicate used to filter events</param>
        /// <returns>The stack event</returns>
        public static Maybe<StackEvent> GetLastStackEvent(this Func<IAmazonCloudFormation> clientFactory, StackArn stack,
            Func<StackEvent, bool> predicate = null)
        {
            try
            {
                return clientFactory()
                    .DescribeStackEvents(new DescribeStackEventsRequest {StackName = stack.Value})
                    .Map(response => response?.StackEvents
                        .OrderByDescending(stackEvent => stackEvent.Timestamp)
                        .FirstOrDefault(stackEvent => predicate == null || predicate(stackEvent))
                    ).AsSome();
            }
            catch (AmazonCloudFormationException ex)
            {
                if (ex.ErrorCode == "AccessDenied")
                {
                    throw new PermissionException(
                        "AWS-CLOUDFORMATION-ERROR-0002: The AWS account used to perform the operation does not have " +
                        "the required permissions to query the current state of the CloudFormation stack. " +
                        "This step will complete without waiting for the stack to complete, and will not fail if the " +
                        "stack finishes in an error state.\n" +
                        ex.Message + "\n" +
                        "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0002");
                }

                return Maybe<StackEvent>.None;
            }
        }

        /// <summary>
        /// Describe the stack
        /// </summary>
        /// <param name="clientFactory"></param>
        /// <param name="stack"></param>
        /// <returns></returns>
        /// <exception cref="PermissionException"></exception>
        /// <exception cref="UnknownException"></exception>
        public static Stack DescribeStack(this Func<IAmazonCloudFormation> clientFactory, StackArn stack)
        {
            try
            {
                return clientFactory()
                    // The client becomes the result of the API call
                    .Map(client => client.DescribeStacks(new DescribeStacksRequest {StackName = stack.Value}))
                    // Get the first stack
                    .Map(response => response.Stacks.FirstOrDefault());
            }
            catch (AmazonCloudFormationException ex)
            {
                if (ex.ErrorCode == "AccessDenied")
                {
                    throw new PermissionException(
                        "AWS-CLOUDFORMATION-ERROR-0004: The AWS account used to perform the operation does not have " +
                        "the required permissions to describe the CloudFormation stack. " +
                        "This means that the step is not able to generate any output variables.\n" +
                        ex.Message + "\n" +
                        "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0004",
                        ex);
                }

                throw new UnknownException(
                    "AWS-CLOUDFORMATION-ERROR-0005: An unrecognised exception was thrown while querying the CloudFormation stacks.\n" +
                    "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0005",
                    ex);
                
            }
        }


        /// <summary>
        /// Check to see if the stack name exists.
        /// </summary>
        /// <param name="clientFactory">The client factory method</param>
        /// <param name="stackArn">The stack name or id</param>
        /// <param name="defaultValue">The default value to return given no permission to query the stack</param>
        /// <returns>The current status of the stack</returns>
        public static StackStatus GetStackStatus(this Func<IAmazonCloudFormation> clientFactory, StackArn stackArn,
            StackStatus defaultValue)
        {
            try
            {
                return clientFactory.DescribeStack(stackArn)
                           // Does the status indicate that processing has finished?
                           ?.Map(stack => (stack.StackStatus?.Value.EndsWith("_COMPLETE") ?? true) ||
                                          stack.StackStatus.Value.EndsWith("_FAILED"))
                           .Map(completed => completed ? StackStatus.Completed : StackStatus.InProgress)
                       ?? StackStatus.DoesNotExist;
            }
            catch (AmazonCloudFormationException ex)
            {
                if (ex.ErrorCode == "AccessDenied")
                {
                    return defaultValue;
                }

                // This is OK, we just return the fact that the stack does not exist.
                // While calling describe stacks and catching exceptions seems dirty,
                // this is how the stack-exists command on the CLI works:
                // https://docs.aws.amazon.com/cli/latest/reference/cloudformation/wait/stack-exists.html
                if (ex.ErrorCode == "ValidationError")
                {
                    return StackStatus.DoesNotExist;
                }

                throw new UnknownException(
                    "AWS-CLOUDFORMATION-ERROR-0006: An unrecognised exception was thrown while checking to see if the CloudFormation stack exists.\n" +
                    "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0006",
                    ex);
            }
        }

        /// <summary>
        /// Get the potentially useful inner web exception message if there is one.
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public static string GetWebExceptionMessage(this AmazonServiceException exception)
        {
            return (exception.InnerException as WebException)?
                   .Response?
                   .GetResponseStream()?
                   .Map(stream => new StreamReader(stream).ReadToEnd())
                   .Map(message => "An exception was thrown while contacting the AWS API.\n" + message)
                   ?? "An exception was thrown while contacting the AWS API.";
        }

        /// <summary>
        /// Wait for a given stack to complete by polling the stack events
        /// </summary>
        /// <param name="clientFactory">The client factory method to use</param>
        /// <param name="waitPeriod">The period to wait between events</param>
        /// <param name="stack">The stack name or id to query</param>
        /// param name="action">Callback for each event while waiting
        /// <param name="filter">The predicate for filtering the stack events</param>
        public static void WaitForStackToComplete(this Func<IAmazonCloudFormation> clientFactory,
            TimeSpan waitPeriod, StackArn stack, Action<Maybe<StackEvent>> action = null, Func<StackEvent, bool> filter= null)
        {
            Guard.NotNull(stack, "Stack should not be null");
            Guard.NotNull(clientFactory, "Client factory should not be null");

            var status = clientFactory.GetStackStatus(stack, StackStatus.DoesNotExist);
            if (status == StackStatus.DoesNotExist || status == StackStatus.Completed)
            {
                return;
            }

            do
            {
                Thread.Sleep(waitPeriod);
                var @event = clientFactory.GetLastStackEvent(stack, filter);
                action?.Invoke(@event);
            } while (clientFactory.GetStackStatus(stack, StackStatus.Completed) == StackStatus.InProgress);
        }
                
        /// <summary>
        /// Check the stack event status to detemrine whether it was successful.
        /// http://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/using-cfn-describing-stacks.html#w2ab2c15c15c17c11
        /// </summary>
        /// <param name="status">The status to check</param>
        /// <returns>true if the status indcates a failed create or update, and false otherwise</returns>
        public static Maybe<bool> MaybeIndicatesSuccess(this StackEvent status)
        {
            return status.ToMaybe().Select(x => !UnsuccessfulStackEvents.Contains(x.ResourceStatus.Value));
        }

        public static bool StackIsUnrecoverable(this StackEvent status)
        {
            Guard.NotNull(status, "Status should not be null");
            return UnrecoverableStackStatuses.Contains(status.ResourceStatus.Value);
        }

        public static DeleteStackResponse DeleteStack(this Func<IAmazonCloudFormation> clientFactory, StackArn stack)
        {
            Guard.NotNull(clientFactory, "clientFactory should not be null");
            Guard.NotNull(stack, "Stack should not be null");

            try
            {
                return clientFactory().Map(client =>
                    client.DeleteStack(new DeleteStackRequest {StackName = stack.Value}));
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
        }

        public static string CreateStack(this Func<IAmazonCloudFormation> clientFactory,
            CreateStackRequest request)
        {
            try
            {
                return clientFactory()
                    .Map(client => client.CreateStack(request))
                    // Narrow the response to the stack ID
                    .Map(response => response.StackId);
            }
            catch (AmazonCloudFormationException ex)
            {
                if (ex.ErrorCode == "AccessDenied")
                {
                    throw new PermissionException(
                        "AWS-CLOUDFORMATION-ERROR-0007: The AWS account used to perform the operation does not have " +
                        "the required permissions to create the stack.\n" +
                        ex.Message + "\n" +
                        "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0007");
                }

                throw new UnknownException(
                    "AWS-CLOUDFORMATION-ERROR-0008: An unrecognised exception was thrown while creating a CloudFormation stack.\n" +
                    "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0008",
                    ex);
            }
        }
    }
}
