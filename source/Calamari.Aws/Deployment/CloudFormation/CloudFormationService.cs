using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Aws.Util;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Octopus.CoreUtilities;

namespace Calamari.Aws.Deployment.CloudFormation
{
    public class CloudFormationService : ICloudFormationService, IDisposable
    {
        readonly StackEventLogger stackEventLogger;
        readonly ILog log;

        readonly Lazy<Task<IAmazonCloudFormation>> amazonCloudFormationClient;

        public CloudFormationService(ILog log, IAmazonClientFactory amazonClientFactory)
        {
            this.log = log;
            
            amazonCloudFormationClient = new Lazy<Task<IAmazonCloudFormation>>(amazonClientFactory.GetCloudFormationClient);
            stackEventLogger = new StackEventLogger(log);
        }

        public async Task ExecuteChangeSet(StackArn stackArn, ChangeSetArn changeSetArn, bool waitForCompletion)
        {
            Guard.NotNull(stackArn, $"'{nameof(stackArn)}' cannot be null.");
            Guard.NotNull(changeSetArn, $"'{nameof(changeSetArn)}' cannot be null.");

            var client = await amazonCloudFormationClient.Value;

            var response = await client.DescribeChangeSetAsync(stackArn, changeSetArn);

            if (!response.Changes.Any())
            {
                await client.DeleteChangeSetAsync(new DeleteChangeSetRequest
                {
                    ChangeSetName = changeSetArn.Value,
                    StackName = stackArn.Value
                });

                log.Info("No changes need to be performed.");
                return;
            }

            await WaitAndExecuteChangeSet(stackArn, changeSetArn);

            if (waitForCompletion)
            {
                await WithAmazonServiceExceptionHandling(async () =>
                    await client.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stackArn, LogAndThrowRollbacks(client, stackArn))
                );
            }
        }

        async Task WaitAndExecuteChangeSet(StackArn stack, ChangeSetArn changeSet)
        {
            try
            {
                var client = await amazonCloudFormationClient.Value;

                var changes = await client.WaitForChangeSetCompletion(CloudFormationDefaults.StatusWaitPeriod,
                    new RunningChangeSet(stack, changeSet));


                if (changes.Status == ChangeSetStatus.FAILED)
                {
                    throw new UnknownException($"The changeset failed to create.\n{changes.StatusReason}");
                }

                await client.ExecuteChangeSetAsync(new ExecuteChangeSetRequest
                {
                    ChangeSetName = changeSet.Value,
                    StackName = stack.Value
                });
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

        public async Task<IReadOnlyCollection<VariableOutput>> GetOutputVariablesByStackArn(StackArn stackArn)
        {
            Guard.NotNull(stackArn, $"'{nameof(stackArn)}' cannot be null.");

            var client = await amazonCloudFormationClient.Value;

            return await GetOutputVariablesWithRetry(() =>
                    WithAmazonServiceExceptionHandling(async () => (await QueryStackAsync(client, stackArn)).ToMaybe()));
        }

        static async Task<(IReadOnlyList<VariableOutput> result, bool success)> GetOutputVariables(
            Func<Task<Maybe<Stack>>> query)
        {
            List<VariableOutput> ConvertStackOutputs(Stack stack) =>
                stack.Outputs.Select(p => new VariableOutput(p.OutputKey, p.OutputValue)).ToList();

            var result = await query();

            return (result: result.Select(ConvertStackOutputs).SomeOr(new List<VariableOutput>()), success: true);
        }

        async Task<IReadOnlyCollection<VariableOutput>> GetOutputVariablesWithRetry(Func<Task<Maybe<Stack>>> query)
        {
            for (var retry = 0; retry < CloudFormationDefaults.RetryCount; ++retry)
            {
                var (variableOutputs, success) = await GetOutputVariables(query);

                if (success) return variableOutputs;

                // Wait for a bit for and try again
                await Task.Delay(CloudFormationDefaults.StatusWaitPeriod);
            }

            return null;
        }

        public async Task DeleteByStackArn(StackArn stackArn, bool waitForCompletion)
        {
            Guard.NotNull(stackArn, $"'{nameof(stackArn)}' cannot be null.");

            var client = await amazonCloudFormationClient.Value;

            if (await client.StackExistsAsync(stackArn, StackStatus.Completed) != StackStatus.DoesNotExist)
            {
                await WithAmazonServiceExceptionHandling(async () =>
                {
                    await client.DeleteStackAsync(stackArn);
                });

                log.Info($"Deleted stackArn called {stackArn.Value} in region {client.Config.RegionEndpoint.SystemName}");
            }
            else
            {
                log.Info($"No stackArn called {stackArn.Value} exists in region {client.Config.RegionEndpoint.SystemName}");
                return;
            }

            if (waitForCompletion)
            {
                await WithAmazonServiceExceptionHandling(async () =>
                {
                    await client.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stackArn,
                        LogAndThrowRollbacks(client, stackArn, true, false));
                });
            }
        }

        public async Task<RunningChangeSet> CreateChangeSet(string changeSetName, CloudFormationTemplate cloudFormationTemplate, StackArn stackArn, string roleArn, IReadOnlyCollection<string> iamCapabilities)
        {
            Guard.NotNullOrWhiteSpace(changeSetName, $"'{nameof(changeSetName)}' cannot be null or white space.");
            Guard.NotNull(cloudFormationTemplate, $"'{nameof(cloudFormationTemplate)}' cannot be null.");
            Guard.NotNull(stackArn, $"'{nameof(stackArn)}' cannot be null.");
            Guard.NotNullOrWhiteSpace(roleArn, $"'{nameof(roleArn)}' cannot be null or white space.");
            Guard.NotNull(iamCapabilities, $"'{nameof(iamCapabilities)}' cannot be null.");

            var client = await amazonCloudFormationClient.Value;

            try
            {
                var stackStatus = await client.StackExistsAsync(stackArn, StackStatus.DoesNotExist);
                var changeSet = await CreateChangeSet(
                    new CreateChangeSetRequest
                    {
                        StackName = stackArn.Value,
                        TemplateBody = cloudFormationTemplate.Content,
                        Parameters = cloudFormationTemplate.Inputs.ToList(),
                        ChangeSetName = changeSetName,
                        ChangeSetType = stackStatus == StackStatus.DoesNotExist ? ChangeSetType.CREATE : ChangeSetType.UPDATE,
                        Capabilities = iamCapabilities.ToList(),
                        RoleARN = roleArn
                    }
                );

                await WaitForChangeSetCompletion(changeSet);

                return changeSet;
            }
            catch (AmazonServiceException exception)
            {
                LogAmazonServiceException(exception);
                throw;
            }
        }

        async Task WaitForChangeSetCompletion(RunningChangeSet result)
        {
            var client = await amazonCloudFormationClient.Value;

            await client.WaitForChangeSetCompletion(CloudFormationDefaults.StatusWaitPeriod, result);
        }

        async Task<RunningChangeSet> CreateChangeSet(CreateChangeSetRequest request)
        {
            try
            {
                var client = await amazonCloudFormationClient.Value;

                var response = await client.CreateChangeSetAsync(request);

                return new RunningChangeSet(new StackArn(response.StackId), new ChangeSetArn(response.Id));
            }
            catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "AccessDenied")
            {
                throw new PermissionException(
                    @"The AWS account used to perform the operation does not have the required permissions to create the change set.\n" +
                    "Please ensure the current user has the cloudformation:CreateChangeSet permission.\n" +
                    ex.Message + "\n", ex);
            }
        }

        public async Task<IReadOnlyCollection<Change>> GetChangeSet(StackArn stackArn, ChangeSetArn changeSetArn)
        {
            Guard.NotNull(stackArn, $"'{nameof(stackArn)}' cannot be null.");
            Guard.NotNull(changeSetArn, $"'{nameof(changeSetArn)}' cannot be null.");

            return await WithAmazonServiceExceptionHandling(async () =>
            {
                try
                {
                    var client = await amazonCloudFormationClient.Value;

                    return (await client.DescribeChangeSetAsync(stackArn, changeSetArn)).Changes;
                }
                catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "AccessDenied")
                {
                    throw new PermissionException(
                        "The AWS account used to perform the operation does not have the required permissions to describe the change set.\n" +
                        "Please ensure the current account has permission to perfrom action 'cloudformation:DescribeChangeSet'." +
                        ex.Message + "\n");
                }
                catch (AmazonCloudFormationException ex)
                {
                    throw new UnknownException("An unrecognized exception was thrown while describing the CloudFormation change set.", ex);
                }
            });
        }

        public async Task<string> Deploy(CloudFormationTemplate cloudFormationTemplate, StackArn stackArn, string roleArn,
            IReadOnlyCollection<string> iamCapabilities, bool isRollbackDisabled, bool waitForCompletion)
        {
            Guard.NotNull(cloudFormationTemplate, $"'{nameof(cloudFormationTemplate)}' cannot be null.");
            Guard.NotNull(stackArn, $"'{nameof(stackArn)}' cannot be null.");
            Guard.NotNullOrWhiteSpace(roleArn, $"'{nameof(roleArn)}' cannot be null or white space.");
            Guard.NotNull(iamCapabilities, $"'{nameof(iamCapabilities)}' cannot be null.");

            var client = await amazonCloudFormationClient.Value;

            await client.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stackArn, LogAndThrowRollbacks(client, stackArn, false));
            
            return await DeployStack(cloudFormationTemplate, stackArn, roleArn, iamCapabilities, isRollbackDisabled, waitForCompletion);
        }

        /// <summary>
        /// Update or create the stackArn
        /// </summary>
        /// <param name="stackArn"></param>
        /// <param name="cloudFormationTemplate"></param>
        async Task<string> DeployStack(CloudFormationTemplate cloudFormationTemplate, StackArn stackArn, string roleArn, IReadOnlyCollection<string> iamCapabilities, bool isRollbackDisabled, bool waitForCompletion)
        {
            var stackStatus = await StackExists(stackArn, StackStatus.DoesNotExist);

            string stackId;

            if (stackStatus != StackStatus.DoesNotExist)
            {
                stackId = await UpdateCloudFormation(cloudFormationTemplate, stackArn, roleArn, iamCapabilities, isRollbackDisabled);
            }
            else
            {
                stackId = await CreateCloudFormation(cloudFormationTemplate, stackArn, roleArn, iamCapabilities, isRollbackDisabled);
            }

            if (waitForCompletion)
            {
                var client = await amazonCloudFormationClient.Value;

                await client.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stackArn, LogAndThrowRollbacks(client, stackArn));
            }

            return stackId;
        }


        /// <summary>
        /// Gets the last stackArn event by timestamp, optionally filtered by a predicate
        /// </summary>
        /// <param name="predicate">The optional predicate used to filter events</param>
        /// <returns>The stackArn event</returns>
        async Task<Maybe<StackEvent>> StackEvent(StackArn stack, Func<StackEvent, bool> predicate = null)
        {
            return await WithAmazonServiceExceptionHandling(
                async () =>
                {
                    var client = await amazonCloudFormationClient.Value;

                    return await client.GetLastStackEvent(stack, predicate);
                });
        }

        /// <summary>
        /// Check to see if the stackArn name exists.
        /// </summary>
        /// <param name="defaultValue">The return value when the user does not have the permissions to query the stacks</param>
        /// <returns>The current status of the stackArn</returns>
        async Task<StackStatus> StackExists(StackArn stack, StackStatus defaultValue)
        {
            var client = await amazonCloudFormationClient.Value;

            return await WithAmazonServiceExceptionHandling(() => client.StackExistsAsync(stack, defaultValue));
        }

        /// <summary>
        /// Creates the stackArn and returns the stackArn ID
        /// </summary>
        /// <param name="template">The CloudFormation cloudFormationTemplate</param>
        /// <returns>The stackArn id</returns>
        async Task<string> CreateCloudFormation(CloudFormationTemplate template, StackArn stackArn, string roleArn, IReadOnlyCollection<string> iamCapabilities, bool isRollbackDisabled)
        {
            var client = await amazonCloudFormationClient.Value;

            return await WithAmazonServiceExceptionHandling(async () =>
            {
                try
                {
                    var stackId = (await client.CreateStackAsync(new CreateStackRequest
                    {
                        StackName = stackArn.Value,
                        TemplateBody = template.Content,
                        Parameters = template.Inputs?.ToList(),
                        Capabilities = iamCapabilities.ToList(),
                        DisableRollback = isRollbackDisabled,
                        RoleARN = roleArn
                    })).StackId;

                    log.Info($"Created stackArn {stackId} in region {client.Config.RegionEndpoint.SystemName}");

                    return stackId;
                }
                catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "AccessDenied")
                {
                    throw new PermissionException(
                        "The AWS account used to perform the operation does not have the required permissions to create the stackArn.\n" +
                        "Please ensure the current account has permission to perform action 'cloudformation:CreateStack'.\n" +
                        ex.Message
                    );
                }
            });
        }

        /// <summary>
        /// Deletes the stackArn
        /// </summary>
        async Task DeleteCloudFormation(StackArn stackArn)
        {
            await WithAmazonServiceExceptionHandling(async () =>
            {
                var client = await amazonCloudFormationClient.Value;

                await client.DeleteStackAsync(stackArn);

                log.Info($"Deleted stackArn called {stackArn.Value} in region {client.Config.RegionEndpoint.SystemName}");
            });
        }

        /// <summary>
        /// Updates the stackArn and returns the stackArn ID
        /// </summary>
        /// <param name="stackArn">The stackArn name or id</param>
        /// <param name="cloudFormationTemplate">The CloudFormation cloudFormationTemplate</param>
        /// <returns>stackId</returns>
        async Task<string> UpdateCloudFormation(
            CloudFormationTemplate cloudFormationTemplate,
            StackArn stackArn,
            string roleArn,
            IReadOnlyCollection<string> iamCapabilities,
            bool isRollbackDisabled)
        {
            var client = await amazonCloudFormationClient.Value;

            try
            {
                var result = await client.UpdateStackAsync(new UpdateStackRequest
                {
                    StackName = stackArn.Value,
                    TemplateBody = cloudFormationTemplate.Content,
                    Parameters = cloudFormationTemplate.Inputs.ToList(),
                    Capabilities = iamCapabilities.ToList(),
                    RoleARN = roleArn
                });

                log.Info(
                    $"Updated stackArn with id {result.StackId} in region {client.Config.RegionEndpoint.SystemName}");

                return result.StackId;
            }
            catch (AmazonCloudFormationException ex)
            {
                // Some stackArn states indicate that we can delete the stackArn and start again. Otherwise we have some other
                // exception that needs to be dealt with.
                if (!(await StackMustBeDeleted(stackArn)).SelectValueOrDefault(x => x))
                {
                    // Is this an unrecoverable state, or just a stackArn that has nothing to update?
                    if (DealWithUpdateException(ex))
                    {
                        // There was nothing to update, but we return the id for consistency anyway
                        var result = await QueryStackAsync(client, stackArn);
                        return result.StackId;
                    }
                }

                // If the stackArn exists, is in a ROLLBACK_COMPLETE state, and was never successfully
                // created in the first place, we can end up here. In this case we try to create
                // the stackArn from scratch.
                await DeleteCloudFormation(stackArn);
                await client.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stackArn, LogAndThrowRollbacks(client, stackArn, false));
                
                return await CreateCloudFormation(cloudFormationTemplate, stackArn, roleArn, iamCapabilities, isRollbackDisabled);
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
        bool DealWithUpdateException(AmazonCloudFormationException ex)
        {
            Guard.NotNull(ex, "ex can not be null");

            // Unfortunately there are no better fields in the exception to use to determine the
            // kind of error than the message. We are forced to match message strings.
            if (ex.Message.Contains("No updates are to be performed"))
            {
                log.Info("No updates are to be performed");
                return true;
            }

            if (ex.ErrorCode == "AccessDenied")
            {
                throw new PermissionException(
                    "The AWS account used to perform the operation does not have the required permissions to update the stackArn.\n" +
                    "Please ensure the current account has permission to perfrom action 'cloudformation:UpdateStack'.\n" +
                    ex.Message);
            }

            throw new UnknownException("An unrecognised exception was thrown while updating a CloudFormation stackArn.\n", ex);
        }

        /// <summary>
        /// Check whether the stackArn must be deleted in order to recover.
        /// </summary>
        /// <param name="stack">The stackArn id or name</param>
        /// <returns>true if this status indicates that the stackArn has to be deleted, and false otherwise</returns>
        async Task<Maybe<bool>> StackMustBeDeleted(StackArn stack)
        {
            try
            {
                return (await StackEvent(stack)).Select(x => x.StackIsUnrecoverable());
            }
            catch (PermissionException)
            {
                // If we can't get the stackArn status, assume it is not in a state that we can recover from
                return Maybe<bool>.None;
            }
        }

        /// <summary>
        /// The AmazonServiceException can hold additional information that is useful to include in
        /// the log.
        /// </summary>
        /// <param name="exception">The exception</param>
        protected void LogAmazonServiceException(AmazonServiceException exception)
        {
            stackEventLogger.Warn("AWS-CLOUDFORMATION-ERROR-0014", exception.GetWebExceptionMessage());
        }

        /// <summary>
        /// Run an action and log any AmazonServiceException detail.
        /// </summary>
        /// <param name="func">The exception</param>
        protected T WithAmazonServiceExceptionHandling<T>(Func<T> func)
        {
            try
            {
                return func();
            }
            catch (AmazonServiceException exception)
            {
                LogAmazonServiceException(exception);
                throw;
            }
        }


        /// <summary>
        /// Display an warning message to the user (without duplicates)
        /// </summary>
        /// <param name="errorCode">The error message code</param>
        /// <param name="message">The error message body</param>
        /// <returns>true if it was displayed, and false otherwise</returns>
        bool DisplayWarning(string errorCode, string message)
        {
            return stackEventLogger.Warn(errorCode, message);
        }

        /// <summary>
        /// Creates a handler which will log stack events and throw on common rollback events
        /// </summary>
        /// <param name="client">The client factory</param>
        /// <param name="stack">The stack to query</param>
        /// <param name="filter">The filter for stack events</param>
        /// <param name="expectSuccess">Whether we expected a success</param>
        /// <param name="missingIsFailure"></param>
        /// <returns>Stack event handler</returns>
        protected Action<Maybe<StackEvent>> LogAndThrowRollbacks(IAmazonCloudFormation client, StackArn stack, bool expectSuccess = true, bool missingIsFailure = true, Func<StackEvent, bool> filter = null)
        {
            return @event =>
            {
                try
                {
                    stackEventLogger.Log(@event);
                    stackEventLogger.LogRollbackError(@event, x =>
                            WithAmazonServiceExceptionHandling(() => client.GetLastStackEvent(stack, x).GetAwaiter().GetResult()),
                        expectSuccess,
                        missingIsFailure);
                }
                catch (PermissionException exception)
                {
                    log.Warn(exception.Message);
                }
            };
        }

        static Task<Stack> QueryStackAsync(IAmazonCloudFormation client, StackArn stack)
        {
            try
            {
                return client.DescribeStackAsync(stack);
            }
            catch (AmazonServiceException ex) when (ex.ErrorCode == "AccessDenied")
            {
                throw new PermissionException(
                    "The AWS account used to perform the operation does not have the required permissions to describe the CloudFormation stack. " +
                    "This means that the step is not able to generate any output variables.\n " +
                    "Please ensure the current account has permission to perform action 'cloudformation:DescribeStacks'.\n" +
                    ex.Message + "\n" +
                    ex);
            }
            catch (AmazonServiceException ex)
            {
                throw new Exception("An unrecognised exception was thrown while querying the CloudFormation stacks.", ex);
            }
        }

        public void Dispose()
        {
            if (amazonCloudFormationClient.IsValueCreated) amazonCloudFormationClient.Value.Dispose();
        }
    }
}
