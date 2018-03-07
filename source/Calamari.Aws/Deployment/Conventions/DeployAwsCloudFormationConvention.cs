using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Util;
using Newtonsoft.Json;
using Octopus.Core.Extensions;

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

        private const int StatusWaitPeriod = 5000;
        private const int RetryCount = 3;
        private static readonly Regex OutputsRe = new Regex("\"?Outputs\"?\\s*:");

        /// <summary>
        /// Matches ARNs like arn:aws:iam::123456789:role/AWSTestRole and extracts the name as group 1 
        /// </summary>
        private static readonly Regex ArnNameRe = new Regex("^.*?/(.+)$");

        private static readonly ITemplateReplacement TemplateReplacement = new TemplateReplacement();

        private readonly string templateFile;
        private readonly string templateParametersFile;
        private readonly bool filesInPackage;
        private readonly ICalamariFileSystem fileSystem;
        private readonly bool waitForComplete;
        private readonly string action;
        private readonly string stackName;
        private readonly bool disableRollback;
        private readonly List<string> capabilities = new List<string>();
        private readonly IAwsEnvironmentGeneration awsEnvironmentGeneration;

        /// <summary>
        /// Track the last status message so we don't fill the logs with redundant information
        /// </summary>
        private string lastMessage;

        /// <summary>
        /// If the user does not have permissions to do something, some warnings are displayed. These
        /// can be displayed multiple times, which is redundant, so this list keeps track of what has
        /// already been shown to the user.
        /// </summary>
        private readonly IList<String> displayedWarnings = new List<String>();

        public DeployAwsCloudFormationConvention(
            string templateFile,
            string templateParametersFile,
            bool filesInPackage,
            string action,
            bool waitForComplete,
            string stackName,
            string iamCapabilities,
            bool disableRollback,
            ICalamariFileSystem fileSystem,
            IAwsEnvironmentGeneration awsEnvironmentGeneration)
        {
            this.templateFile = templateFile;
            this.templateParametersFile = templateParametersFile;
            this.filesInPackage = filesInPackage;
            this.fileSystem = fileSystem;
            this.waitForComplete = waitForComplete;
            this.action = action;
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

            if ("Delete".Equals(action, StringComparison.InvariantCultureIgnoreCase))
            {
                RemoveCloudFormation(deployment);
            }
            else
            {
                DeployCloudFormation(deployment);
            }
        }

        private void DeployCloudFormation(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            WriteCredentialInfo(deployment);

            WaitForStackToComplete(deployment, false);

            TemplateReplacement.ResolveAndSubstituteFile(
                    fileSystem,
                    templateFile,
                    filesInPackage,
                    deployment.Variables)
                .Tee(template => DeployStack(deployment, template));

            GetOutputVars(deployment);
        }

        private void RemoveCloudFormation(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            if (StackExists(StackStatus.Completed) != StackStatus.DoesNotExist)
            {
                DeleteCloudFormation();
            }
            else
            {
                Log.Info(
                    $"No stack called {stackName} exists in region {awsEnvironmentGeneration.AwsRegion.SystemName}");
            }

            if (waitForComplete)
            {
                WaitForStackToComplete(deployment, true, false);
            }
        }

        /// <summary>
        /// Convert the parameters file to a list of parameters
        /// </summary>
        /// <param name="deployment">The current deployment</param>
        /// <returns>The AWS parameters</returns>
        private List<Parameter> GetParameters(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            if (string.IsNullOrWhiteSpace(templateParametersFile))
            {
                return null;
            }

            var retValue = TemplateReplacement.ResolveAndSubstituteFile(
                    fileSystem,
                    templateParametersFile,
                    filesInPackage,
                    deployment.Variables)
                .Map(JsonConvert.DeserializeObject<List<Parameter>>);

            return retValue;
        }

        /// <summary>
        /// Update or create the stack
        /// </summary>
        /// <param name="deployment">The current deployment</param>
        /// <param name="template">The cloudformation template</param>
        private void DeployStack(RunningDeployment deployment, string template)
        {
            Guard.NotNullOrWhiteSpace(template, "template can not be null or empty");
            Guard.NotNull(deployment, "deployment can not be null");

            var stackId = GetParameters(deployment)
                // Use the parameters to either create or update the stack
                .Map(parameters => StackExists(StackStatus.DoesNotExist) != StackStatus.DoesNotExist
                    ? UpdateCloudFormation(deployment, template, parameters)
                    : CreateCloudFormation(template, parameters));

            if (waitForComplete) WaitForStackToComplete(deployment);

            // Take the stack ID returned by the create or update events, and save it as an output variable
            Log.SetOutputVariable("AwsOutputs[StackId]", stackId, deployment.Variables);
            Log.Info(
                $"Saving variable \"Octopus.Action[{deployment.Variables["Octopus.Action.Name"]}].Output.AwsOutputs[StackId]\"");
        }

        /// <summary>
        /// Prints some info about the user or role that is running the deployment
        /// </summary>
        /// <param name="deployment">The current deployment</param>
        private void WriteCredentialInfo(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            if (deployment.Variables.IsSet(SpecialVariables.Action.Aws.AssumeRoleARN) ||
                !deployment.Variables.IsSet(SpecialVariables.Action.Aws.AccountId) ||
                !deployment.Variables.IsSet(deployment.Variables.Get(SpecialVariables.Action.Aws.AccountId) +
                                            ".AccessKey"))
            {
                WriteRoleInfo();
            }
            else
            {
                WriteUserInfo();
            }
        }

        /// <summary>
        /// Attempt to get the output variables, taking into account whether any were defined in the template,
        /// and if we are to wait for the deployment to finish.
        /// </summary>
        /// <param name="deployment">The current deployment</param>
        private void GetOutputVars(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            // Try a few times to get the outputs (if there were any in the template file)
            for (var retry = 0; retry < RetryCount; ++retry)
            {
                var successflyReadOutputs = TemplateFileContainsOutputs(templateFile, deployment)
                    // take the result of our scan of the template, and use it to determine
                    // if we need to wait for outputs to be created
                    .Map(outputsDefined =>
                        QueryStack()?.Outputs
                            // For each output, save it as an output variable, and change the agregated value to true
                            // to indicate that we have successfully extracted an output variable
                            .Aggregate(false, (success, output) =>
                            {
                                Log.SetOutputVariable($"AwsOutputs[{output.OutputKey}]",
                                    output.OutputValue, deployment.Variables);
                                Log.Info(
                                    $"Saving variable \"Octopus.Action[{deployment.Variables["Octopus.Action.Name"]}].Output.AwsOutputs[{output.OutputKey}]\"");
                                return true;
                            }) ??
                        // Or if there were no outputs to wait for, then we have successfully read the (lack of) outputs
                        !outputsDefined
                    );

                // If we have read the outputs, or we are not waiting, exit
                if (successflyReadOutputs || !waitForComplete)
                {
                    break;
                }

                // Wait for a bit for and try again
                Thread.Sleep(StatusWaitPeriod);
            }
        }

        /// <summary>
        /// Look at the template file and see if there were any outputs.
        /// </summary>
        /// <param name="template">The template file</param>
        /// <param name="deployment">The current deployment</param>
        /// <returns>true if the Outputs marker was found, and false otherwise</returns>
        private bool TemplateFileContainsOutputs(string template, RunningDeployment deployment)
        {
            Guard.NotNullOrWhiteSpace(template, "template can not be null or empty");
            Guard.NotNull(deployment, "deployment can not be null");

            return TemplateReplacement.GetAbsolutePath(
                    fileSystem,
                    template,
                    filesInPackage,
                    deployment.Variables)
                // The path is transformed to the string contents
                .Map(path => fileSystem.ReadFile(path))
                // The contents becomes true or false based on the regex match
                .Map(contents => OutputsRe.IsMatch(contents));
        }

        /// <summary>
        /// Dump the details of the current user's assumed role.
        /// </summary>
        private void WriteRoleInfo()
        {
            try
            {
                new AmazonSecurityTokenServiceClient(awsEnvironmentGeneration.AwsCredentials,
                        new AmazonSecurityTokenServiceConfig
                        {
                            RegionEndpoint = awsEnvironmentGeneration.AwsRegion,
                            ProxyPort = awsEnvironmentGeneration.ProxyPort,
                            ProxyCredentials = awsEnvironmentGeneration.ProxyCredentials,
                            ProxyHost = awsEnvironmentGeneration.ProxyHost
                        })
                    // Client becomes the response of the API call
                    .Map(client => client.GetCallerIdentity(new GetCallerIdentityRequest()))
                    // The response is narrowed to the Aen
                    .Map(response => response.Arn)
                    // Try and match the response to get just the role
                    .Map(arn => ArnNameRe.Match(arn))
                    // Extract the role name, or a default
                    .Map(match => match.Success ? match.Groups[1].Value : "Unknown")
                    // Log the output
                    .Tee(role => Log.Info($"Running the step as the AWS role {role}"));
            }
            catch (AmazonServiceException)
            {
                // Ignore, we just won't add this to the logs
            }
        }

        /// <summary>
        /// Dump the details of the current user.
        /// </summary>
        private void WriteUserInfo()
        {
            try
            {
                new AmazonIdentityManagementServiceClient(awsEnvironmentGeneration.AwsCredentials,
                        new AmazonIdentityManagementServiceConfig
                        {
                            RegionEndpoint = awsEnvironmentGeneration.AwsRegion,
                            ProxyPort = awsEnvironmentGeneration.ProxyPort,
                            ProxyCredentials = awsEnvironmentGeneration.ProxyCredentials,
                            ProxyHost = awsEnvironmentGeneration.ProxyHost
                        })
                    // The client becomes the API response
                    .Map(client => client.GetUser(new GetUserRequest()))
                    // Log the details of the response
                    .Tee(response => Log.Info($"Running the step as the AWS user {response.User.UserName}"));
            }
            catch (AmazonServiceException)
            {
                // Ignore, we just won't add this to the logs
            }
        }


        /// <summary>
        /// Query the stack for the outputs
        /// </summary>
        /// <returns>The output variables</returns>
        private Stack QueryStack()
        {
            try
            {
                return new AmazonCloudFormationClient(awsEnvironmentGeneration.AwsCredentials,
                        new AmazonCloudFormationConfig
                        {
                            RegionEndpoint = awsEnvironmentGeneration.AwsRegion,
                            ProxyPort = awsEnvironmentGeneration.ProxyPort,
                            ProxyCredentials = awsEnvironmentGeneration.ProxyCredentials,
                            ProxyHost = awsEnvironmentGeneration.ProxyHost
                        })
                    // The client becomes the result of the API call
                    .Map(client => client.DescribeStacks(new DescribeStacksRequest() {StackName = stackName}))
                    // Get the first stack
                    .Map(response => response.Stacks.FirstOrDefault());
            }
            catch (AmazonServiceException ex)
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
        /// Wait for the stack to be in a completed state
        /// </summary>
        /// <param name="deployment">The current deployment</param>
        /// <param name="expectSuccess">True if we expect to see a successful status result, false otherwise</param>
        /// <param name="missingIsFailure">True if the a missing stack indicates a failure, and false otherwise</param>
        private void WaitForStackToComplete(
            RunningDeployment deployment,
            bool expectSuccess = true,
            bool missingIsFailure = true)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            if (StackExists(StackStatus.DoesNotExist) == StackStatus.DoesNotExist ||
                StackExists(StackStatus.DoesNotExist) == StackStatus.Completed)
            {
                return;
            }

            do
            {
                Thread.Sleep(StatusWaitPeriod);
                StackEventCompleted(deployment, expectSuccess, missingIsFailure);
            } while (StackExists(StackStatus.Completed) == StackStatus.InProgress);
        }

        /// <summary>
        /// Gets the last stack event by timestamp, optionally filtered by a predicate
        /// </summary>
        /// <param name="predicate">The optional predicate used to filter events</param>
        /// <returns>The stack event</returns>
        private StackEvent StackEvent(Func<StackEvent, bool> predicate = null)
        {
            return new AmazonCloudFormationClient(awsEnvironmentGeneration.AwsCredentials,
                    new AmazonCloudFormationConfig
                    {
                        RegionEndpoint = awsEnvironmentGeneration.AwsRegion,
                        ProxyPort = awsEnvironmentGeneration.ProxyPort,
                        ProxyCredentials = awsEnvironmentGeneration.ProxyCredentials,
                        ProxyHost = awsEnvironmentGeneration.ProxyHost
                    })
                .Map(client =>
                {
                    try
                    {
                        return client.DescribeStackEvents(new DescribeStackEventsRequest() {StackName = stackName});
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

                        // Assume this is a "Stack [StackName] does not exist" error
                        return null;
                    }
                    catch (AmazonServiceException ex)
                    {
                        HandleAmazonServiceException(ex);
                        throw ex;
                    }
                })
                .Map(response => response?.StackEvents
                    .OrderByDescending(stackEvent => stackEvent.Timestamp)
                    .FirstOrDefault(stackEvent => predicate == null ||
                                                  predicate(stackEvent)));
        }

        /// <summary>
        /// Queries the state of the stack, and checks to see if it is in a completed state
        /// </summary>
        /// <param name="expectSuccess">True if we were expecting this event to indicate success</param>
        /// <param name="deployment">The current deployment</param>
        /// <param name="missingIsFailure">True if the a missing stack indicates a failure, and false otherwise</param>
        /// <returns>True if the stack is completed or no longer available, and false otherwise</returns>
        private bool StackEventCompleted(
            RunningDeployment deployment,
            bool expectSuccess = true,
            bool missingIsFailure = true)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            try
            {
                return StackEvent()
                    // Log the details of the status event
                    .Tee(LogCurrentStates)
                    // Check to see if we have any errors in the status
                    .Tee(status => LogRollbackError(deployment, status, expectSuccess, missingIsFailure))
                    // convert the status to true/false based on the presense of these suffixes
                    .Map(status => ((status?.ResourceStatus.Value.EndsWith("_COMPLETE") ?? true) ||
                                    (status.ResourceStatus.Value.EndsWith("_FAILED"))) &&
                                   (status?.ResourceType.Equals("AWS::CloudFormation::Stack") ?? true));
            }
            catch (PermissionException ex)
            {
                Log.Warn(ex.Message);
                return true;
            }
        }

        /// <summary>
        /// Write the state of the stack, but only if it changed since last time. If we are
        /// writing the same message more than once, do it as verbose logging.
        /// </summary>
        /// <param name="status">The current status of the stack</param>
        private void LogCurrentStates(StackEvent status)
        {
            var statusMessage =
                $"{status?.ResourceType.Map(type => type + " ")}{status?.ResourceStatus.Value ?? "Does not exist"}";
            if (statusMessage != lastMessage)
            {
                Log.Info($"Current stack state: {statusMessage}");
            }
            else
            {
                Log.Verbose($"Current stack state: {statusMessage}");
            }

            lastMessage = statusMessage;
        }

        /// <summary>
        /// Log an error if we expected success and got a rollback
        /// </summary>
        /// <param name="status">The status of the stack, or null if the stack does not exist</param>
        /// <param name="expectSuccess">True if the status should indicate success</param>
        /// <param name="missingIsFailure">True if the a missing stack indicates a failure, and false otherwise</param>
        /// <param name="deployment">The current deployment</param>
        private void LogRollbackError(
            RunningDeployment deployment,
            StackEvent status,
            bool expectSuccess,
            bool missingIsFailure)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            var isUnsuccessful = StatusIsUnsuccessfulResult(status, missingIsFailure);
            var isStackType = status?.ResourceType.Equals("AWS::CloudFormation::Stack") ?? true;

            if (expectSuccess && isUnsuccessful && isStackType)
            {
                Log.Warn(
                    "Stack was either missing, in a rollback state, or in a failed state. This means that the stack was not processed correctly. " +
                    "Review the stack in the AWS console to find any errors that may have occured during deployment.");
                try
                {
                    var progressStatus = StackEvent(stack => stack.ResourceStatusReason != null);
                    if (progressStatus != null)
                    {
                        Log.Warn(progressStatus.ResourceStatusReason);
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

        /// <summary>
        /// Check to see if the stack name exists.
        /// </summary>
        /// <param name="defaultValue">The return value when the user does not have the permissions to query the stacks</param>
        /// <returns>The current status of the stack</returns>
        private StackStatus StackExists(StackStatus defaultValue)
        {
            try
            {
                return new AmazonCloudFormationClient(awsEnvironmentGeneration.AwsCredentials,
                               new AmazonCloudFormationConfig
                               {
                                   RegionEndpoint = awsEnvironmentGeneration.AwsRegion,
                                   ProxyPort = awsEnvironmentGeneration.ProxyPort,
                                   ProxyCredentials = awsEnvironmentGeneration.ProxyCredentials,
                                   ProxyHost = awsEnvironmentGeneration.ProxyHost
                               })
                           // The client becomes the result of the API call
                           .Map(client => client.DescribeStacks(new DescribeStacksRequest{StackName = stackName}))
                           // The result becomes true/false based on the presence of a matching stack name
                           .Map(result => result.Stacks.FirstOrDefault())
                           // Does the status indicate that processing has finished?
                           ?.Map(stack => (stack.StackStatus?.Value.EndsWith("_COMPLETE") ?? true) ||
                                          (stack.StackStatus.Value.EndsWith("_FAILED")))
                           // Convert the result to a StackStatus
                           .Map(completed => completed ? StackStatus.Completed : StackStatus.InProgress)
                       // Of, if there was no stack that matched the name, the stack does not exist
                       ?? StackStatus.DoesNotExist;
            }
            catch (AmazonCloudFormationException ex)
            {
                if (ex.ErrorCode == "AccessDenied")
                {
                    DisplayWarning(
                        "AWS-CLOUDFORMATION-ERROR-0003",
                        "The AWS account used to perform the operation does not have " +
                        "the required permissions to describe the stack.\n" +
                        ex.Message);

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
            catch (AmazonServiceException ex)
            {
                HandleAmazonServiceException(ex);
                throw ex;
            }
        }

        /// <summary>
        /// Creates the stack and returns the stack ID
        /// </summary>
        /// <param name="template">The CloudFormation template</param>
        /// <param name="parameters">The parameters JSON file</param>
        /// <returns>The stack id</returns>
        private string CreateCloudFormation(string template, List<Parameter> parameters)
        {
            Guard.NotNullOrWhiteSpace(template, "template can not be null or empty");

            try
            {
                return new AmazonCloudFormationClient(awsEnvironmentGeneration.AwsCredentials,
                        new AmazonCloudFormationConfig
                        {
                            RegionEndpoint = awsEnvironmentGeneration.AwsRegion,
                            ProxyPort = awsEnvironmentGeneration.ProxyPort,
                            ProxyCredentials = awsEnvironmentGeneration.ProxyCredentials,
                            ProxyHost = awsEnvironmentGeneration.ProxyHost
                        })
                    // Client becomes the API response
                    .Map(client => client.CreateStack(
                        new CreateStackRequest()
                        {
                            StackName = stackName,
                            TemplateBody = template,
                            Parameters = parameters,
                            Capabilities = capabilities,
                            DisableRollback = disableRollback
                        }))
                    // Narrow the response to the stack ID
                    .Map(response => response.StackId)
                    // Log the stack id
                    .Tee(stackId =>
                        Log.Info(
                            $"Created stack with id {stackId} in region {awsEnvironmentGeneration.AwsRegion.SystemName}"));
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
            catch (AmazonServiceException ex)
            {
                HandleAmazonServiceException(ex);
                throw ex;
            }
        }

        /// <summary>
        /// Deletes the stack
        /// </summary>
        private void DeleteCloudFormation()
        {
            try
            {
                new AmazonCloudFormationClient(awsEnvironmentGeneration.AwsCredentials,
                        new AmazonCloudFormationConfig
                        {
                            RegionEndpoint = awsEnvironmentGeneration.AwsRegion,
                            ProxyPort = awsEnvironmentGeneration.ProxyPort,
                            ProxyCredentials = awsEnvironmentGeneration.ProxyCredentials,
                            ProxyHost = awsEnvironmentGeneration.ProxyHost
                        })
                    // Client becomes the API response
                    .Map(client => client.DeleteStack(new DeleteStackRequest() {StackName = stackName}))
                    // Log the response details
                    .Tee(status =>
                        Log.Info(
                            $"Deleted stack called {stackName} in region {awsEnvironmentGeneration.AwsRegion.SystemName}"));
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
            catch (AmazonServiceException ex)
            {
                HandleAmazonServiceException(ex);
                throw ex;
            }
        }

        /// <summary>
        /// Updates the stack and returns the stack ID
        /// </summary>
        /// <param name="template">The CloudFormation template</param>
        /// <param name="parameters">The parameters JSON file</param>
        /// <param name="deployment">The current deployment</param>
        /// <returns>stackId</returns>
        private string UpdateCloudFormation(
            RunningDeployment deployment,
            string template,
            List<Parameter> parameters)
        {
            Guard.NotNullOrWhiteSpace(template, "template can not be null or empty");
            Guard.NotNull(deployment, "deployment can not be null");

            try
            {
                return new AmazonCloudFormationClient(awsEnvironmentGeneration.AwsCredentials,
                        new AmazonCloudFormationConfig
                        {
                            RegionEndpoint = awsEnvironmentGeneration.AwsRegion,
                            ProxyPort = awsEnvironmentGeneration.ProxyPort,
                            ProxyCredentials = awsEnvironmentGeneration.ProxyCredentials,
                            ProxyHost = awsEnvironmentGeneration.ProxyHost
                        })
                    // Client becomes the API response
                    .Map(client => client.UpdateStack(
                        new UpdateStackRequest()
                        {
                            StackName = stackName,
                            TemplateBody = template,
                            Parameters = parameters,
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
                if (!StackMustBeDeleted(false))
                {
                    // Is this an unrecoverable state, or just a stack that has nothing to update?
                    if (DealWithUpdateException(ex))
                    {
                        // There was nothing to update, but we return the id for consistency anyway
                        return QueryStack().StackId;
                    }
                }

                // If the stack exists, is in a ROLLBACK_COMPLETE state, and was never successfully
                // created in the first place, we can end up here. In this case we try to create
                // the stack from scratch.
                DeleteCloudFormation();
                WaitForStackToComplete(deployment, false);
                return CreateCloudFormation(template, parameters);
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
        /// <param name="defaultValue">the default value if the status is null</param>
        /// <returns>true if this status indicates that the stack has to be deleted, and false otherwise</returns>
        private bool StackMustBeDeleted(bool defaultValue)
        {
            try
            {
                return new[]
                    {
                        "CREATE_FAILED", "ROLLBACK_COMPLETE", "ROLLBACK_FAILED", "DELETE_FAILED",
                        "UPDATE_ROLLBACK_FAILED"
                    }
                    .Any(
                        x => StackEvent()?.ResourceStatus.Value
                                 .Equals(x, StringComparison.InvariantCultureIgnoreCase) ??
                             defaultValue);
            }
            catch (PermissionException)
            {
                // If we can't get the stack status, assume it is not in a state that we can recover from
                return false;
            }
        }

        /// <summary>
        /// These status indicate that an update or create was not successful.
        /// http://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/using-cfn-describing-stacks.html#w2ab2c15c15c17c11
        /// </summary>
        /// <param name="status">The status to check</param>
        /// <param name="defaultValue">The default value if status is null</param>
        /// <returns>true if the status indcates a failed create or update, and false otherwise</returns>
        private bool StatusIsUnsuccessfulResult(StackEvent status, bool defaultValue)
        {
            return new[]
            {
                "CREATE_ROLLBACK_COMPLETE", "CREATE_ROLLBACK_FAILED", "UPDATE_ROLLBACK_COMPLETE",
                "UPDATE_ROLLBACK_FAILED", "ROLLBACK_COMPLETE", "ROLLBACK_FAILED", "DELETE_FAILED",
                "CREATE_FAILED"
            }.Any(x =>
                status?.ResourceStatus.Value.Equals(x, StringComparison.InvariantCultureIgnoreCase) ??
                defaultValue);
        }

        /// <summary>
        /// Display an warning message to the user (without duplicates)
        /// </summary>
        /// <param name="errorCode">The error message code</param>
        /// <param name="message">The error message body</param>
        /// <returns>true if it was displayed, and false otherwise</returns>
        private bool DisplayWarning(string errorCode, string message)
        {
            if (!displayedWarnings.Contains(errorCode))
            {
                displayedWarnings.Add(errorCode);
                Log.Warn(
                    errorCode + ": " + message + "\n" +
                    "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#" +
                    errorCode.ToLower());
                return true;
            }

            return false;
        }

        /// <summary>
        /// The AmazonServiceException can hold additional information that is useful to include in
        /// the log.
        /// </summary>
        /// <param name="exception">The exception</param>
        private void HandleAmazonServiceException(AmazonServiceException exception)
        {
            ((exception.InnerException as WebException)?
             .Response?
             .GetResponseStream()?
             .Map(stream => new StreamReader(stream).ReadToEnd())
             .Map(message => "An exception was thrown while contacting the AWS API.\n" + message)
             ?? "An exception was thrown while contacting the AWS API.")
             .Tee(message => DisplayWarning("AWS-CLOUDFORMATION-ERROR-0014", message));
        }
    }
}