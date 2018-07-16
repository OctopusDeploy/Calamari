using System;
using System.Collections.Generic;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Exceptions;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Integration.CloudFormation
{
    public class StackEventLogger
    {
        private readonly ILog log;
        private string lastMessage;
        private HashSet<string> warnings = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        public StackEventLogger(ILog log)
        {
            this.log = log;
        }

        public void Warn(string errorCode, string message)
        {
            if (!warnings.Contains(errorCode))
            {
                warnings.Add(errorCode);
                log.Warn( errorCode + ": " + message + "\n" +
                          "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#" +
                          errorCode.ToLower());
            }
        }
        
        
        /// <summary>
        /// Write the state of the stack, but only if it changed since last time. If we are
        /// writing the same message more than once, do it as verbose logging.
        /// </summary>
        /// <param name="status">The current status of the stack</param>
        public void Log(StackEvent status)
        {
            var statusMessage =
                $"{status?.ResourceType.Map(type => type + " ")}{status?.ResourceStatus.Value ?? "Does not exist"}";
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
            StackEvent status,
            Func<Func<StackEvent, bool>, StackEvent> query,
            bool expectSuccess = true,
            bool missingIsFailure = true)
        {
            var isUnsuccessful = status.IndicatesSuccess().SelectValueOr(x => x, missingIsFailure);
            var isStackType = status?.ResourceType.Equals("AWS::CloudFormation::Stack") ?? true;

            if (expectSuccess && isUnsuccessful && isStackType)
            {
                log.Warn(
                    "Stack was either missing, in a rollback state, or in a failed state. This means that the stack was not processed correctly. " +
                    "Review the stack in the AWS console to find any errors that may have occured during deployment.");
                try
                {
                    var progressStatus = query(stack => stack.ResourceStatusReason != null);
                    if (progressStatus != null)
                    {
                        log.Warn(progressStatus.ResourceStatusReason);
                    }
                }
                catch (PermissionException)
                {
                    // ignore, it just means we won't display any of the status reasons
                }

                throw new RollbackException(
                    "AWS-CLOUDFORMATION-ERROR-0001: CloudFormation stack finished in a rollback or failed state. " +
                    "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0001");
            }
        }
    }
}