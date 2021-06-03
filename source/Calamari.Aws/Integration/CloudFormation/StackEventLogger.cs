using System;
using System.Collections.Generic;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Exceptions;
using Calamari.Common.Plumbing.Logging;
using Octopus.CoreUtilities;

namespace Calamari.Aws.Integration.CloudFormation
{
    public class StackEventLogger
    {
        private readonly ILog log;
        private string lastMessage;
        private HashSet<string> warnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public StackEventLogger(ILog log)
        {
            this.log = log;
        }

        /// <summary>
        /// Display an warning message to the user (without duplicates)
        /// </summary>
        /// <param name="errorCode">The error message code</param>
        /// <param name="message">The error message body</param>
        /// <returns>true if it was displayed, and false otherwise</returns>
        public bool Warn(string errorCode, string message)
        {
            if (!warnings.Contains(errorCode))
            {
                warnings.Add(errorCode);
                log.Warn($"{errorCode}: {message}\nFor more information visit {log.FormatLink($"https://g.octopushq.com/AwsCloudFormationDeploy#{errorCode.ToLower()}")}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Write the state of the stack, but only if it changed since last time. If we are
        /// writing the same message more than once, do it as verbose logging.
        /// </summary>
        /// <param name="status">The current status of the stack</param>
        public void Log(Maybe<StackEvent> status)
        {
            var statusMessage = status.SelectValueOrDefault(x => $"{x.LogicalResourceId} - {x.ResourceType} - {x.ResourceStatus.Value ?? "Does not exist"}{(x.ResourceStatusReason != null ? $" - {x.ResourceStatusReason}" : "")}");
            if (statusMessage != lastMessage)
            {
                log.Info($"Current stack state: {statusMessage}");
            }
            else
            {
                log.Verbose($"Current stack state: {statusMessage}");
            }

            lastMessage = statusMessage;
        }

        /// <summary>
        /// Log an error if we expected success and got a rollback
        /// </summary>
        /// <param name="status">The status of the stack, or null if the stack does not exist</param>
        /// <param name="expectSuccess">True if the status should indicate success</param>
        /// <param name="missingIsFailure">True if the a missing stack indicates a failure, and false otherwise</param>
        public void LogRollbackError(
            Maybe<StackEvent> status,
            Func<Func<StackEvent, bool>, List<Maybe<StackEvent>>> query,
            bool expectSuccess,
            bool missingIsFailure)
        {
            var isSuccess = status.Select(x => x.MaybeIndicatesSuccess()).SelectValueOr(x => x.Value, !missingIsFailure);
            var isStackType = status.SelectValueOr(x => x.ResourceType.Equals("AWS::CloudFormation::Stack"), true);

            if (expectSuccess && !isSuccess && isStackType)
            {
                log.Warn(
                    status.SelectValueOr(x =>
                            $"Stack status {x.ResourceStatus.Value} indicated rollback or failed state. ",
                            "Stack was unexpectedly missing during processing. ") +
                        "This means that the stack was not processed correctly. Review the stack events logged below or check the stack in the AWS console to find any errors that may have occured during deployment."
                    );
                try
                {
                    var progressStatuses = query(stack => stack?.ResourceStatusReason != null);

                    foreach (var progressStatus in progressStatuses)
                    {
                        if (progressStatus.Some())
                        {
                            var progressStatusSuccess = progressStatus.Select(s => s.MaybeIndicatesSuccess()).SelectValueOr(x => x.Value, true);
                            var progressStatusMessage = $"Stack {progressStatus.Value.StackId)} event: {progressStatus.Value.Timestamp:u} - {progressStatus.Value.LogicalResourceId} - {progressStatus.Value.ResourceType} - {progressStatus.Value.ResourceStatus} - {progressStatus.Value.ResourceStatusReason}";
                            if (progressStatusSuccess)
                                log.Verbose(progressStatusMessage);
                            else
                                log.Warn(progressStatusMessage);
                        }
                    }
                }
                catch (PermissionException)
                {
                    // ignore, it just means we won't display any of the status reasons
                }

                throw new RollbackException(
                    "AWS-CLOUDFORMATION-ERROR-0001: CloudFormation stack finished in a rollback or failed state. " +
                    $"For more information visit {log.FormatLink("https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0001")}");
            }
        }
    }
}