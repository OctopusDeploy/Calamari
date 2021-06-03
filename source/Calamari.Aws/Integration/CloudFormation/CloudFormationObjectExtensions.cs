using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Calamari.Aws.Exceptions;
using Calamari.Common.Plumbing;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;
using StackStatus = Calamari.Aws.Deployment.Conventions.StackStatus;

namespace Calamari.Aws.Integration.CloudFormation
{
    public static class CloudFormationObjectExtensions
    {
        private static readonly HashSet<string> RecognisedCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CAPABILITY_IAM", "CAPABILITY_NAMED_IAM", "CAPABILITY_AUTO_EXPAND"
        };

        // These status indicate that an update or create was not successful.
        // http://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/using-cfn-describing-stacks.html#w2ab2c15c15c17c11
        private static HashSet<string> UnsuccessfulStackEvents =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CREATE_ROLLBACK_COMPLETE",
                "CREATE_ROLLBACK_FAILED",
                "UPDATE_ROLLBACK_COMPLETE",
                "UPDATE_ROLLBACK_FAILED",
                "ROLLBACK_COMPLETE",
                "ROLLBACK_FAILED",
                "DELETE_FAILED",
                "CREATE_FAILED",
                "UPDATE_FAILED"
            };

        /// Some statuses indicate that the only way forward is to delete the stack and try again.
        /// Here are some of the explanations of the stack states from the docs.
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
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CREATE_FAILED", "ROLLBACK_COMPLETE", "ROLLBACK_FAILED", "DELETE_FAILED",
                "UPDATE_ROLLBACK_FAILED"
            };

        public static Task<DescribeChangeSetResponse> DescribeChangeSetAsync(this Func<IAmazonCloudFormation> factory,
            StackArn stack, ChangeSetArn changeSet)
        {
            return factory().DescribeChangeSetAsync(new DescribeChangeSetRequest
            {
                ChangeSetName = changeSet.Value,
                StackName = stack.Value
            });
        }

        public static Task<CreateChangeSetResponse> CreateChangeSetAsync(this Func<IAmazonCloudFormation> factory, CreateChangeSetRequest request)
        {
            return factory().CreateChangeSetAsync(request);
        }

        public static async Task<DescribeChangeSetResponse> WaitForChangeSetCompletion(
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
                var result = await clientFactory.DescribeChangeSetAsync(runningChangeSet.Stack, runningChangeSet.ChangeSet);
                if (completion.Contains(result.Status))
                {
                    return result;
                }

                await Task.Delay(waitPeriod);
            }
        }

        /// <summary>
        /// Gets the last stack event by timestamp, optionally filtered by a predicate
        /// </summary>
        /// <param name="predicate">The optional predicate used to filter events</param>
        /// <returns>The stack event</returns>
        public static async Task<Maybe<StackEvent>> GetLastStackEvent(this Func<IAmazonCloudFormation> clientFactory,
            StackArn stack,
            Func<StackEvent, bool> predicate = null)
        {
            try
            {
                var response = await clientFactory().DescribeStackEventsAsync(new DescribeStackEventsRequest { StackName = stack.Value });

                return response?
                    .StackEvents.OrderByDescending(stackEvent => stackEvent.Timestamp)
                    .FirstOrDefault(stackEvent => predicate == null || predicate(stackEvent))
                    .AsSome();
            }
            catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "AccessDenied")
            {
                throw new PermissionException(
                    "The AWS account used to perform the operation does not have the required permissions to query the current state of the CloudFormation stack. " +
                    "This step will complete without waiting for the stack to complete, and will not fail if the stack finishes in an error state.\n " +
                    "Please ensure the current account has permission to perform action 'cloudformation:DescribeStackEvents'" +
                    ex.Message);
            }
            catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "ExpiredToken")
            {
                throw new PermissionException(
                    "Security token has expired. Please increase the session duration and/or check that the system date and time are set correctly. " +
                    ex.Message);
            }
            catch (AmazonCloudFormationException)
            {
                return Maybe<StackEvent>.None;
            }
        }

        /// <summary>
        /// Gets all stack events, optionally filtered by a predicate
        /// </summary>
        /// <param name="predicate">The optional predicate used to filter events</param>
        /// <returns>The stack events</returns>
        public static async Task<List<Maybe<StackEvent>>> GetStackEvents(this Func<IAmazonCloudFormation> clientFactory,
            StackArn stack,
            Func<StackEvent, bool> predicate = null)
        {
            try
            {
                var stackEventResults = new List<Maybe<StackEvent>>();

                var response = await clientFactory().DescribeStackEventsAsync(new DescribeStackEventsRequest { StackName = stack.Value });

                var stackEvents = response?
                    .StackEvents.Where(stackEvent => predicate == null || predicate(stackEvent))                    
                    .ToList();

                stackEventResults.AddRange(stackEvents.Select(s => s.AsSome()));

                var nestedStackIds = stackEvents
                    .Where(s => s.ResourceType == "AWS::CloudFormation::Stack" && s.PhysicalResourceId != s.StackId)
                    .Select(s => s.PhysicalResourceId)
                    .Distinct()
                    .ToList();

                foreach (var nestedStackId in nestedStackIds)
                {
                    var nestedStackEvents = await GetStackEvents(clientFactory, new StackArn(nestedStackId), predicate);
                    if (nestedStackEvents.Any())
                    {
                        stackEventResults.AddRange(nestedStackEvents);
                    }
                }

                return stackEventResults.OrderBy(s => s.SelectValueOr(e => e.Timestamp, DateTime.MinValue)).ToList();
            }
            catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "AccessDenied")
            {
                throw new PermissionException(
                    "The AWS account used to perform the operation does not have the required permissions to query the current state of the CloudFormation stack. " +
                    "This step will complete without waiting for the stack to complete, and will not fail if the stack finishes in an error state.\n " +
                    "Please ensure the current account has permission to perform action 'cloudformation:DescribeStackEvents'" +
                    ex.Message);
            }
            catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "ExpiredToken")
            {
                throw new PermissionException(
                    "Security token has expired. Please increase the session duration and/or check that the system date and time are set correctly. " +
                    ex.Message);
            }
            catch (AmazonCloudFormationException)
            {
                return new List<Maybe<StackEvent>> { Maybe<StackEvent>.None };
            }
        }

        /// <summary>
        /// Describe the stack
        /// </summary>
        /// <param name="clientFactory"></param>
        /// <param name="stack"></param>
        /// <returns></returns>
        public static async Task<Stack> DescribeStackAsync(this Func<IAmazonCloudFormation> clientFactory, StackArn stack)
        {
            var response = await clientFactory().DescribeStacksAsync(new DescribeStacksRequest { StackName = stack.Value });
            return response.Stacks.FirstOrDefault();
        }


        /// <summary>
        /// Check to see if the stack name exists.
        /// </summary>
        /// <param name="clientFactory">The client factory method</param>
        /// <param name="stackArn">The stack name or id</param>
        /// <param name="defaultValue">The default value to return given no permission to query the stack</param>
        /// <returns>The current status of the stack</returns>
        public static async Task<StackStatus> StackExistsAsync(this Func<IAmazonCloudFormation> clientFactory, StackArn stackArn,
            StackStatus defaultValue)
        {
            try
            {
                var result = await clientFactory.DescribeStackAsync(stackArn);

                if (result == null)
                {
                    return StackStatus.DoesNotExist;
                }

                if (result.StackStatus == null ||
                    result.StackStatus.Value.EndsWith("_COMPLETE") ||
                    result.StackStatus.Value.EndsWith("_FAILED"))
                {
                    return StackStatus.Completed;
                }

                return StackStatus.InProgress;
            }
            catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "AccessDenied")
            {
                return defaultValue;
            }
            catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "ValidationError")
            {
                return StackStatus.DoesNotExist;
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
        public static async Task WaitForStackToComplete(this Func<IAmazonCloudFormation> clientFactory,
            TimeSpan waitPeriod, StackArn stack, Action<Maybe<StackEvent>> action = null, Func<StackEvent, bool> filter = null)
        {
            Guard.NotNull(stack, "Stack should not be null");
            Guard.NotNull(clientFactory, "Client factory should not be null");

            var status = await clientFactory.StackExistsAsync(stack, StackStatus.DoesNotExist);
            if (status == StackStatus.DoesNotExist || status == StackStatus.Completed)
            {
                return;
            }

            do
            {
                await Task.Delay(waitPeriod);
                var @event = await clientFactory.GetLastStackEvent(stack, filter);
                action?.Invoke(@event);
            } while (await clientFactory.StackExistsAsync(stack, StackStatus.Completed) == StackStatus.InProgress);
        }

        /// <summary>
        /// Check the stack event status to determine whether it was successful.
        /// http://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/using-cfn-describing-stacks.html#w2ab2c15c15c17c11
        /// </summary>
        /// <param name="status">The status to check</param>
        /// <returns>true if the status indicates a failed create or update, and false otherwise</returns>
        public static Maybe<bool> MaybeIndicatesSuccess(this StackEvent status)
        {
            return status.ToMaybe().Select(x => !UnsuccessfulStackEvents.Contains(x.ResourceStatus.Value));
        }

        public static bool StackIsUnrecoverable(this StackEvent status)
        {
            Guard.NotNull(status, "Status should not be null");
            return UnrecoverableStackStatuses.Contains(status.ResourceStatus.Value);
        }

        public static Task<DeleteStackResponse> DeleteStackAsync(this Func<IAmazonCloudFormation> clientFactory, StackArn stack)
        {
            Guard.NotNull(clientFactory, "clientFactory should not be null");
            Guard.NotNull(stack, "Stack should not be null");

            try
            {
                return clientFactory().DeleteStackAsync(new DeleteStackRequest { StackName = stack.Value });
            }
            catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "AccessDenied")
            {
                throw new PermissionException(
                    "The AWS account used to perform the operation does not have the required permissions to delete the stack.\n" +
                    "Please ensure the current account has permission to perform action 'cloudformation:DeleteStack'.\n" +
                    ex.Message
                );
            }
        }

        public static async Task<string> CreateStackAsync(this Func<IAmazonCloudFormation> clientFactory,
            CreateStackRequest request)
        {
            try
            {
                var response = await clientFactory().CreateStackAsync(request);
                return response.StackId;
            }
            catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "AccessDenied")
            {
                throw new PermissionException(
                    "The AWS account used to perform the operation does not have the required permissions to create the stack.\n" +
                    "Please ensure the current account has permission to perform action 'cloudformation:CreateStack'.\n" +
                    ex.Message
                );
            }
        }

        // https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/using-iam-template.html#capabilities
        public static bool IsKnownIamCapability(this string capability)
        {
            return RecognisedCapabilities.Contains(capability);
        }

    }
}
