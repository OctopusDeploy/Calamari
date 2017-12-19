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
        private static readonly Regex OutputsRE = new Regex("\"?Outputs\"?\\s*:");
        /// <summary>
        /// Matches ARNs like arn:aws:iam::123456789:role/AWSTestRole and extracts the name as group 1 
        /// </summary>
        private static readonly Regex ARNNameRE = new Regex("^.*?/(.+)$");
        private static readonly ITemplateReplacement TemplateReplacement = new TemplateReplacement();

        readonly string templateFile;
        readonly string templateParametersFile;
        private readonly bool filesInPackage;
        readonly ICalamariFileSystem fileSystem;
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
            var variables = deployment.Variables;
            var stackName = variables[SpecialVariables.Action.Aws.CloudFormationStackName];

            var template = TemplateReplacement.ResolveAndSubstituteFile(
                fileSystem,
                templateFile,
                filesInPackage,
                variables);
            var parameters = !string.IsNullOrWhiteSpace(templateParametersFile)
                ? TemplateReplacement.ResolveAndSubstituteFile(
                        fileSystem,
                        templateParametersFile,
                        filesInPackage,
                        variables)
                    .Map(JsonConvert.DeserializeObject<List<Parameter>>)
                : null;

            WriteCredentialInfo(deployment);

            WaitForStackToComplete(deployment, stackName, false);

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
                .Tee(stackId => Log.SetOutputVariable($"AwsOutputs[StackId]", stackId, variables));

            GetOutputVars(stackName, deployment);
        }

        /// <summary>
        /// Prints some info about the user or role that is running the deployment
        /// </summary>
        /// <param name="deployment">The current deployment</param>
        private void WriteCredentialInfo(RunningDeployment deployment)
        {
            if (deployment.Variables.IsSet(SpecialVariables.Action.Aws.AssumeRoleARN))
            {
                WriteRoleInfo(deployment);
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
        private bool TemplateFileContainsOutputs(string template, RunningDeployment deployment) =>
            TemplateReplacement.GetAbsolutePath(
                    fileSystem,
                    templateParametersFile,
                    filesInPackage,
                    deployment.Variables)
                .Map(path => fileSystem.ReadFile(path))
                .Map(contents => OutputsRE.IsMatch(contents));

        /// <summary>
        /// Build the credentials all AWS clients will use
        /// </summary>
        /// <returns>The credentials used by the AWS clients</returns>
        private AWSCredentials GetCredentials() => new EnvironmentVariablesAWSCredentials();

        /// <summary>
        /// Dump the details of the current user's assumed role.
        /// </summary>
        private void WriteRoleInfo(RunningDeployment deployment) =>
            deployment.Variables[SpecialVariables.Action.Aws.AssumeRoleARN]
                .Map(arn => ARNNameRE.Match(arn))
                .Map(match => match.Success ? match.Groups[1].Value : "Unknown")
                .Tee(role => Log.Info($"Running the step as the AWS role {role}"));

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
        private Stack QueryStack(string stackName) =>
            new AmazonCloudFormationClient(GetCredentials())
                .Map(client => client.DescribeStacks(new DescribeStacksRequest()
                    .Tee(request => { request.StackName = stackName; })))
                .Map(response => response.Stacks.FirstOrDefault());

        /// <summary>
        /// Wait for the stack to be in a completed state
        /// </summary>
        /// <param name="stackName">The name of the stack</param>
        private void WaitForStackToComplete(RunningDeployment deployment, string stackName, bool expectSuccess = true)
        {
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

        private StackEvent StackEvent(string stackName, Regex status = null) =>
            new AmazonCloudFormationClient(GetCredentials())
                .Map(client =>
                {
                    try
                    {
                        return client.DescribeStackEvents(new DescribeStackEventsRequest()
                            .Tee(request => { request.StackName = stackName; }));
                    }
                    catch (AmazonCloudFormationException ex)
                    {
                        // Assume this is a "Stack [StackName] does not exist" error
                        return null;
                    }
                })
                .Map(response => response?.StackEvents
                    .OrderByDescending(stackEvent => stackEvent.Timestamp)
                    .FirstOrDefault(stackEvent => status == null ||
                                                  status.IsMatch(stackEvent.ResourceStatus.Value)));

        /// <summary>
        /// Queries the state of the stack, and checks to see if it is in a completed state
        /// </summary>
        /// <param name="stackName">The name of the stack</param>
        /// <param name="expectSuccess">True if we were expecting this event to indicate success</param>
        /// <returns>True if the stack is completed or no longer available, and false otherwise</returns>
        private Boolean StackEventCompleted(RunningDeployment deployment, string stackName, bool expectSuccess = true) =>
            StackEvent(stackName)
                .Tee(status =>
                    Log.Info($"Current stack state: {status?.ResourceType.Map(type => type + " ")}" +
                             $"{status?.ResourceStatus.Value ?? "Does not exist"}"))
                .Tee(status => LogRollbackError(deployment, status, stackName, expectSuccess))
                .Map(status => ((status?.ResourceStatus.Value.EndsWith("_COMPLETE") ?? true) ||
                               (status?.ResourceStatus.Value.EndsWith("_FAILED") ?? true)) &&
                               (status?.ResourceType.Equals("AWS::CloudFormation::Stack") ?? true));

        /// <summary>
        /// Log an error if we expected success and got a rollback
        /// </summary>
        /// <param name="status"></param>
        /// <param name="stackName"></param>
        /// <param name="expectSuccess"></param>
        private void LogRollbackError(RunningDeployment deployment, StackEvent status, string stackName, bool expectSuccess)
        {
            var isRollback = StatusIsCreateOrUpdateRollback(status, true);
            var isStackType = status?.ResourceType.Equals("AWS::CloudFormation::Stack") ?? true;

            if (expectSuccess && isRollback && isStackType)
            {
                Log.Warn(
                    "Stack was either missing or in a rollback state. This may mean that the stack was not processed correctly. " +
                    "Review the stack in the AWS console to find any errors that may have occured during deployment.");
                var progressStatus = StackEvent(stackName, new Regex(".*?ROLLBACK_IN_PROGRESS"));
                if (progressStatus != null)
                {
                    Log.Warn(progressStatus.ResourceStatusReason);
                }

                if ("True".Equals(deployment.Variables[SpecialVariables.Action.Aws.FailOnRollback], StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new RollbackException("CloudFormation stack finished in a rollback state, and the \"Fail on rollback\" option was enabled.");
                }
            }
        }

        /// <summary>
        /// Check to see if the stack name exists
        /// </summary>
        /// <param name="stackName">The name of the stack</param>
        /// <returns>True if the stack exists, and false otherwise</returns>
        private Boolean StackExists(string stackName) => new AmazonCloudFormationClient()
            .Map(client => client.DescribeStacks(new DescribeStacksRequest()))
            .Map(result => result.Stacks.Any(stack => stack.StackName == stackName));

        /// <summary>
        /// Creates the stack and returns the stack ID
        /// </summary>
        /// <param name="stackName">The name of the stack to create</param>
        /// <param name="template">The CloudFormation template</param>
        /// <param name="parameters">The parameters JSON file</param>
        /// <returns></returns>
        private string CreateCloudFormation(string stackName, string template, List<Parameter> parameters) =>
            new AmazonCloudFormationClient(GetCredentials())
                .Map(client => client.CreateStack(
                    new CreateStackRequest().Tee(request =>
                    {
                        request.StackName = stackName;
                        request.TemplateBody = template;
                        request.Parameters = parameters;
                    })))
                .Map(response => response.StackId)
                .Tee(stackId => Log.Info($"Created stack with id {stackId}"));

        /// <summary>
        /// Deletes the stack and returns the stack ID
        /// </summary>
        /// <param name="stackName">The name of the stack to delete</param>
        /// <returns></returns>
        private void DeleteCloudFormation(string stackName) =>
            new AmazonCloudFormationClient(GetCredentials())
                .Map(client => client.DeleteStack(
                    new DeleteStackRequest().Tee(request => request.StackName = stackName)))
                .Tee(response => Log.Info($"Deleted stack called {stackName}"));

        /// <summary>
        /// Updates the stack and returns the stack ID
        /// </summary>
        /// <param name="stackName">The name of the stack to create</param>
        /// <param name="template">The CloudFormation template</param>
        /// <param name="parameters">The parameters JSON file</param>
        /// <returns></returns>
        private string UpdateCloudFormation(RunningDeployment deployment, string stackName, string template, List<Parameter> parameters)
        {
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
        private bool StatusIsRollback(StackEvent status, bool defaultValue) =>
            new[] {"ROLLBACK_COMPLETE", "ROLLBACK_FAILED"}.Any(x =>
                status?.ResourceStatus.Value.Equals(x, StringComparison.InvariantCultureIgnoreCase) ?? defaultValue);
        
        /// <summary>
        /// These status indicate that an update or create was not successful.
        /// http://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/using-cfn-describing-stacks.html#w2ab2c15c15c17c11
        /// </summary>
        /// <param name="status">The status to check</param>
        /// <param name="defaultValue">The default value if status is null</param>
        /// <returns>true if the status indcates a failed create or update, and false otherwise</returns>
        private bool StatusIsCreateOrUpdateRollback(StackEvent status, bool defaultValue) =>
            new[] {"CREATE_ROLLBACK_COMPLETE", "CREATE_ROLLBACK_FAILED", "UPDATE_ROLLBACK_COMPLETE", "UPDATE_ROLLBACK_FAILED", "ROLLBACK_COMPLETE", "ROLLBACK_FAILED"}.Any(x =>
                status?.ResourceStatus.Value.Equals(x, StringComparison.InvariantCultureIgnoreCase) ?? defaultValue);
    }
}