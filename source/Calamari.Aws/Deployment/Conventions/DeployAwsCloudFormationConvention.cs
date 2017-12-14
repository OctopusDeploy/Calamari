using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
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
        private static readonly ITemplateReplacement TemplateReplacement = new TemplateReplacement();

        readonly string templateFile;
        readonly string templateParametersFile;
        private readonly bool filesInPackage;
        readonly ICalamariFileSystem fileSystem;

        public DeployAwsCloudFormationConvention(
            string templateFile,
            string templateParametersFile,
            bool filesInPackage,
            ICalamariFileSystem fileSystem)
        {
            this.templateFile = templateFile;
            this.templateParametersFile = templateParametersFile;
            this.filesInPackage = filesInPackage;
            this.fileSystem = fileSystem;
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

            WaitForStackToComplete(stackName);
            
            (StackExists(stackName)
                    ? UpdateCloudFormation(stackName, template, parameters)
                    : CreateCloudFormation(stackName, template, parameters))
                .Tee(stackId => Log.SetOutputVariable($"AwsOutputs[StackId]", stackId, variables));

            QueryStackOutputs(stackName)
                ?.ForEach(output =>
                {
                    Log.SetOutputVariable($"AwsOutputs[{output.OutputKey}]", output.OutputValue, variables);
                    Log.Info($"Saving variable \"AwsOutputs[{output.OutputKey}]\"");
                });
        }

        /// <summary>
        /// Query the stack for the outputs
        /// </summary>
        /// <param name="stackName">The name of the stack</param>
        /// <returns>The output variables</returns>
        private List<Output> QueryStackOutputs(string stackName) =>
            new AmazonCloudFormationClient()
                .Map(client => client.DescribeStacks(new DescribeStacksRequest()
                    .Tee(request => { request.StackName = stackName; })))
                .Map(response => response.Stacks.FirstOrDefault())
                .Map(stack => stack?.Outputs);

        /// <summary>
        /// Wait for the stack to be in a completed state
        /// </summary>
        /// <param name="stackName">The name of the stack</param>
        private void WaitForStackToComplete(string stackName)
        {
            if (!StackExists(stackName))
            {
                return;
            }

            do
            {
                Thread.Sleep(5);
            } while (!StackEventCompleted(stackName));
        }

        /// <summary>
        /// Queries the state of the stack, and checks to see if it is in a completed state
        /// </summary>
        /// <param name="stackName">The name of the stack</param>
        /// <returns>True if the stack is completed or no longer available, and false otherwise</returns>
        private Boolean StackEventCompleted(string stackName) =>
            new AmazonCloudFormationClient()
                .Map(client => client.DescribeStackEvents(new DescribeStackEventsRequest()
                    .Tee(request => { request.StackName = stackName; })))
                .Map(response => response.StackEvents.FirstOrDefault())
                .Tee(response => response?.Tee(r => Log.Info($"Current stack state: {r.ResourceStatus.Value}")))
                .Map(stackEvent => stackEvent == null || stackEvent.ResourceStatus.Value.EndsWith("_COMPLETE"));

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
            new AmazonCloudFormationClient()
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
        /// Updates the stack and returns the stack ID
        /// </summary>
        /// <param name="stackName">The name of the stack to create</param>
        /// <param name="template">The CloudFormation template</param>
        /// <param name="parameters">The parameters JSON file</param>
        /// <returns></returns>
        private string UpdateCloudFormation(string stackName, string template, List<Parameter> parameters) =>
            new AmazonCloudFormationClient()
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
}