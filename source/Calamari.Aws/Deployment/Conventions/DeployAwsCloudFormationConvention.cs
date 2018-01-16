using System;
using System.Collections.Generic;
using System.Linq;
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
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Util;
using Newtonsoft.Json;
using Octopus.Core.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class DeployAwsCloudFormationConvention : IInstallConvention
    {
        private const int StatusWaitPeriod = 15000;
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

        public DeployAwsCloudFormationConvention(
            string templateFile,
            string templateParametersFile,
            bool filesInPackage,
            bool waitForComplete,
            ICalamariFileSystem fileSystem)
        {
            this.templateFile = templateFile;
            this.templateParametersFile = templateParametersFile;
            this.filesInPackage = filesInPackage;
            this.fileSystem = fileSystem;
            this.waitForComplete = waitForComplete;
        }

        public void Install(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            if (deployment.Variables[SpecialVariables.Action.Aws.CloudFormationAction]
                .Map(variable => "Delete".Equals(variable, StringComparison.InvariantCultureIgnoreCase)))
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

            var stackName = deployment.Variables[SpecialVariables.Action.Aws.CloudFormationStackName];

            WriteCredentialInfo(deployment);

            WaitForStackToComplete(deployment, stackName, false);

            TemplateReplacement.ResolveAndSubstituteFile(
                    fileSystem,
                    templateFile,
                    filesInPackage,
                    deployment.Variables)
                .Tee(template => DeployStack(stackName, deployment, template));

            GetOutputVars(stackName, deployment);
        }

        private void RemoveCloudFormation(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");
            
            deployment.Variables[SpecialVariables.Action.Aws.CloudFormationStackName]
                .Tee(DeleteCloudFormation)
                .Tee(stackName =>
                {
                    if (waitForComplete)
                        WaitForStackToComplete(deployment, stackName);
                });            
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
        /// <param name="stackName">The name of the stack</param>
        /// <param name="deployment">The current deployment</param>
        /// <param name="template">The cloudformation template</param>
        private void DeployStack(string stackName, RunningDeployment deployment, string template)
        {
            Guard.NotNullOrWhiteSpace(stackName, "stackName can not be null or empty");
            Guard.NotNullOrWhiteSpace(template, "template can not be null or empty");
            Guard.NotNull(deployment, "deployment can not be null");

            GetParameters(deployment)
                .Tee(parameters =>
                    (StackExists(stackName)
                        ? UpdateCloudFormation(deployment, stackName, template, parameters)
                        : CreateCloudFormation(stackName, template, parameters))
                    .Tee(stackId =>
                    {
                        // If we should do so, wait for the stack to complete before saving the stack id.
                        // This means variuable save log messages will be grouped together
                        if (waitForComplete) WaitForStackToComplete(deployment, stackName);
                    })
                    .Tee(stackId =>
                        Log.Info(
                            $"Saving variable \"Octopus.Action[{deployment.Variables["Octopus.Action.Name"]}].Output.AwsOutputs[StackId]\""))
                    .Tee(stackId => Log.SetOutputVariable("AwsOutputs[StackId]", stackId, deployment.Variables)));
        }

        /// <summary>
        /// Prints some info about the user or role that is running the deployment
        /// </summary>
        /// <param name="deployment">The current deployment</param>
        private void WriteCredentialInfo(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            if (deployment.Variables.IsSet(SpecialVariables.Action.Aws.AssumeRoleARN))
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
        /// <param name="stackName">The name of the stack</param>
        /// <param name="deployment">The current deployment</param>
        private void GetOutputVars(string stackName, RunningDeployment deployment)
        {
            Guard.NotNullOrWhiteSpace(stackName, "stackName can not be null or empty");
            Guard.NotNull(deployment, "deployment can not be null");

            // Try a few times to get the outputs (if there were any in the template file)
            for (var retry = 0; retry < RetryCount; ++retry)
            {
                var successflyReadOutputs = TemplateFileContainsOutputs(templateFile, deployment)
                    .Map(outputsDefined =>
                        QueryStack(stackName)
                            ?.Outputs.Aggregate(false, (success, output) =>
                            {
                                Log.SetOutputVariable($"AwsOutputs[{output.OutputKey}]",
                                    output.OutputValue, deployment.Variables);
                                Log.Info(
                                    $"Saving variable \"Octopus.Action[{deployment.Variables["Octopus.Action.Name"]}].Output.AwsOutputs[{output.OutputKey}]\"");
                                return true;
                            }) ?? !outputsDefined
                    );

                if (successflyReadOutputs || !waitForComplete)
                {
                    break;
                }

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
                    templateParametersFile,
                    filesInPackage,
                    deployment.Variables)
                .Map(path => fileSystem.ReadFile(path))
                .Map(contents => OutputsRe.IsMatch(contents));
        }

        /// <summary>
        /// Build the credentials all AWS clients will use
        /// </summary>
        /// <returns>The credentials used by the AWS clients</returns>
        private static AWSCredentials GetCredentials() => new EnvironmentVariablesAWSCredentials();

        /// <summary>
        /// Dump the details of the current user's assumed role.
        /// </summary>
        private void WriteRoleInfo()
        {
            new AmazonSecurityTokenServiceClient(GetCredentials())
                .Map(client => client.GetCallerIdentity(new GetCallerIdentityRequest()))
                .Map(response => response.Arn)
                .Map(arn => ArnNameRe.Match(arn))
                .Map(match => match.Success ? match.Groups[1].Value : "Unknown")
                .Tee(role => Log.Info($"Running the step as the AWS role {role}"));
        }

        /// <summary>
        /// Dump the details of the current user.
        /// </summary>
        private void WriteUserInfo() =>
            new AmazonIdentityManagementServiceClient(GetCredentials())
                .Map(client => client.GetUser(new GetUserRequest()))
                .Tee(response => Log.Info($"Running the step as the AWS user {response.User.UserName}"));


        /// <summary>
        /// Query the stack for the outputs
        /// </summary>
        /// <param name="stackName">The name of the stack</param>
        /// <returns>The output variables</returns>
        private Stack QueryStack(string stackName)
        {
            Guard.NotNullOrWhiteSpace(stackName, "stackName can not be null or empty");

            return new AmazonCloudFormationClient(GetCredentials())
                .Map(client => client.DescribeStacks(new DescribeStacksRequest()
                    .Tee(request => { request.StackName = stackName; })))
                .Map(response => response.Stacks.FirstOrDefault());
        }


        /// <summary>
        /// Wait for the stack to be in a completed state
        /// </summary>
        /// <param name="stackName">The name of the stack</param>
        /// <param name="deployment">The current deployment</param>
        /// <param name="expectSuccess">True if we expect to see a successful status result, false otherwise</param>
        private void WaitForStackToComplete(RunningDeployment deployment, string stackName, bool expectSuccess = true)
        {
            Guard.NotNull(deployment, "deployment can not be null");
            Guard.NotNullOrWhiteSpace(stackName, "stackName can not be null or empty");

            if (!StackExists(stackName))
            {
                return;
            }

            do
            {
                Thread.Sleep(StatusWaitPeriod);
            } while (!StackEventCompleted(deployment, stackName, expectSuccess));

            Thread.Sleep(StatusWaitPeriod);
        }

        /// <summary>
        /// Gets the last stack event by timestamp, optionally filtered by a predicate
        /// </summary>
        /// <param name="stackName">The name of the stack to query</param>
        /// <param name="predicate">The optional predicate used to filter events</param>
        /// <returns></returns>
        private StackEvent StackEvent(string stackName, Func<StackEvent, bool> predicate = null)
        {
            Guard.NotNullOrWhiteSpace(stackName, "stackName can not be null or empty");

            return new AmazonCloudFormationClient(GetCredentials())
                .Map(client =>
                {
                    try
                    {
                        return client.DescribeStackEvents(new DescribeStackEventsRequest()
                            .Tee(request => { request.StackName = stackName; }));
                    }
                    catch (AmazonCloudFormationException)
                    {
                        // Assume this is a "Stack [StackName] does not exist" error
                        return null;
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
        /// <param name="stackName">The name of the stack</param>
        /// <param name="expectSuccess">True if we were expecting this event to indicate success</param>
        /// <param name="deployment">The current deployment</param>
        /// <returns>True if the stack is completed or no longer available, and false otherwise</returns>
        private bool StackEventCompleted(RunningDeployment deployment, string stackName,
            bool expectSuccess = true)
        {
            Guard.NotNull(deployment, "deployment can not be null");
            Guard.NotNullOrWhiteSpace(stackName, "stackName can not be null or empty");

            return StackEvent(stackName)
                .Tee(status =>
                    Log.Info($"Current stack state: {status?.ResourceType.Map(type => type + " ")}" +
                             $"{status?.ResourceStatus.Value ?? "Does not exist"}"))
                .Tee(status => LogRollbackError(deployment, status, stackName, expectSuccess))
                .Map(status => ((status?.ResourceStatus.Value.EndsWith("_COMPLETE") ?? true) ||
                                (status.ResourceStatus.Value.EndsWith("_FAILED"))) &&
                               (status?.ResourceType.Equals("AWS::CloudFormation::Stack") ?? true));
        }

        /// <summary>
        /// Log an error if we expected success and got a rollback
        /// </summary>
        /// <param name="status">The status of the stack, or null if the stack does not exist</param>
        /// <param name="stackName">The name of the stack</param>
        /// <param name="expectSuccess">True if the status should indicate success</param>
        /// <param name="deployment">The current deployment</param>
        private void LogRollbackError(RunningDeployment deployment, StackEvent status, string stackName,
            bool expectSuccess)
        {
            Guard.NotNull(deployment, "deployment can not be null");
            Guard.NotNullOrWhiteSpace(stackName, "stackName can not be null or empty");

            var isRollback = StatusIsCreateOrUpdateRollback(status, true);
            var isStackType = status?.ResourceType.Equals("AWS::CloudFormation::Stack") ?? true;

            if (expectSuccess && isRollback && isStackType)
            {
                Log.Warn(
                    "Stack was either missing or in a rollback state. This means that the stack was not processed correctly. " +
                    "Review the stack in the AWS console to find any errors that may have occured during deployment.");
                var progressStatus = StackEvent(stackName, stack => stack.ResourceStatusReason != null);
                if (progressStatus != null)
                {
                    Log.Warn(progressStatus.ResourceStatusReason);
                }

                throw new RollbackException("AWS-CLOUDFORMATION-ERROR-0001: CloudFormation stack finished in a rollback state. " +
                                            "https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0001");
                
            }
        }

        /// <summary>
        /// Check to see if the stack name exists
        /// </summary>
        /// <param name="stackName">The name of the stack</param>
        /// <returns>True if the stack exists, and false otherwise</returns>
        private Boolean StackExists(string stackName)
        {
            Guard.NotNullOrWhiteSpace(stackName, "stackName can not be null or empty");

            return new AmazonCloudFormationClient()
                .Map(client => client.DescribeStacks(new DescribeStacksRequest()))
                .Map(result => result.Stacks.Any(stack => stack.StackName == stackName));
        }

        /// <summary>
        /// Creates the stack and returns the stack ID
        /// </summary>
        /// <param name="stackName">The name of the stack to create</param>
        /// <param name="template">The CloudFormation template</param>
        /// <param name="parameters">The parameters JSON file</param>
        /// <returns></returns>
        private string CreateCloudFormation(string stackName, string template, List<Parameter> parameters)
        {
            Guard.NotNullOrWhiteSpace(stackName, "stackName can not be null or empty");
            Guard.NotNullOrWhiteSpace(template, "template can not be null or empty");

            return new AmazonCloudFormationClient(GetCredentials())
                .Map(client => client.CreateStack(
                    new CreateStackRequest().Tee(request =>
                    {
                        request.StackName = stackName;
                        request.TemplateBody = template;
                        request.Parameters = parameters;
                    })))
                .Map(response => response.StackId)
                .Tee(stackId => Log.Info($"Created stack with id {stackId}"));
        }

        /// <summary>
        /// Deletes the stack and returns the stack ID
        /// </summary>
        /// <param name="stackName">The name of the stack to delete</param>
        /// <returns></returns>
        private void DeleteCloudFormation(string stackName)
        {
            Guard.NotNullOrWhiteSpace(stackName, "stackName can not be null or empty");

            new AmazonCloudFormationClient(GetCredentials())
                .Map(client => client.DeleteStack(
                    new DeleteStackRequest().Tee(request => request.StackName = stackName)))
                .Tee(response => Log.Info($"Deleted stack called {stackName}"));
        }

        /// <summary>
        /// Updates the stack and returns the stack ID
        /// </summary>
        /// <param name="stackName">The name of the stack to create</param>
        /// <param name="template">The CloudFormation template</param>
        /// <param name="parameters">The parameters JSON file</param>
        /// <param name="deployment">The current deployment</param>
        /// <returns></returns>
        private string UpdateCloudFormation(
            RunningDeployment deployment,
            string stackName,
            string template,
            List<Parameter> parameters)
        {
            Guard.NotNullOrWhiteSpace(stackName, "stackName can not be null or empty");
            Guard.NotNullOrWhiteSpace(template, "template can not be null or empty");
            Guard.NotNull(deployment, "deployment can not be null");

            try
            {
                return new AmazonCloudFormationClient(GetCredentials())
                    .Map(client => client.UpdateStack(
                        new UpdateStackRequest().Tee(request =>
                        {
                            request.StackName = stackName;
                            request.TemplateBody = template;
                            request.Parameters = parameters;
                        })))
                    .Map(response => response.StackId)
                    .Tee(stackId => Log.Info($"Updated stack with id {stackId}"));
            }
            catch (AmazonCloudFormationException ex)
            {
                if (!StatusIsRollback(StackEvent(stackName), false))
                {
                    if (DealWithUpdateException(ex))
                    {
                        // There was nothing to update, but we return the id for consistency anyway
                        return QueryStack(stackName).StackId;
                    }
                }

                // If the stack exists, is in a ROLLBACK_COMPLETE state, and was never successfully
                // created in the first place, we can end up here. In this case we try to create
                // the stack from scratch.
                DeleteCloudFormation(stackName);
                WaitForStackToComplete(deployment, stackName, false);
                return CreateCloudFormation(stackName, template, parameters);
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

            throw ex;
        }

        /// <summary>
        /// Some statuses indicate that the only way forward is to delete the stack and try again.
        /// http://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/using-cfn-describing-stacks.html#w2ab2c15c15c17c11
        /// </summary>
        /// <param name="status">The status to check</param>
        /// <param name="defaultValue">the default value if the status is null</param>
        /// <returns>true if this status indicates that the stack has to be deleted, and false otherwise</returns>
        private bool StatusIsRollback(StackEvent status, bool defaultValue)
        {
            return new[] {"ROLLBACK_COMPLETE", "ROLLBACK_FAILED"}.Any(x =>
                status?.ResourceStatus.Value.Equals(x, StringComparison.InvariantCultureIgnoreCase) ?? defaultValue);
        }

        /// <summary>
        /// These status indicate that an update or create was not successful.
        /// http://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/using-cfn-describing-stacks.html#w2ab2c15c15c17c11
        /// </summary>
        /// <param name="status">The status to check</param>
        /// <param name="defaultValue">The default value if status is null</param>
        /// <returns>true if the status indcates a failed create or update, and false otherwise</returns>
        private bool StatusIsCreateOrUpdateRollback(StackEvent status, bool defaultValue)
        {
            return new[]
            {
                "CREATE_ROLLBACK_COMPLETE", "CREATE_ROLLBACK_FAILED", "UPDATE_ROLLBACK_COMPLETE",
                "UPDATE_ROLLBACK_FAILED", "ROLLBACK_COMPLETE", "ROLLBACK_FAILED"
            }.Any(x =>
                status?.ResourceStatus.Value.Equals(x, StringComparison.InvariantCultureIgnoreCase) ?? defaultValue);
        }
    }
}