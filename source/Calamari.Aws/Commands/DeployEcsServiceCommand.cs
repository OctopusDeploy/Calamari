using System;
using System.Collections.Generic;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Deployment;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.Ecs.CloudFormation;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Newtonsoft.Json;
using Tag = Amazon.CloudFormation.Model.Tag;

namespace Calamari.Aws.Commands
{
    [Command("deploy-aws-ecs-service", Description = "Deploys a service to an Amazon ECS cluster")]
    public class DeployEcsServiceCommand : Command
    {
        readonly ILog log;
        readonly IVariables variables;

        public DeployEcsServiceCommand(ILog log, IVariables variables)
        {
            this.log = log;
            this.variables = variables;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var environment = AwsEnvironmentGeneration.Create(log, variables).GetAwaiter().GetResult();
            var inputs = ReadAndValidateInputs();

            log.Verbose($"Deploying CloudFormation template:\n{inputs.TemplateBody}");

            var stackArn = new StackArn(inputs.StackName);
            var running = new RunningDeployment(variables);

            new ConventionProcessor(running,
            [
                new LogAwsUserInfoConvention(environment),
                new CheckEcsStackNotInProgressConvention(environment, inputs.StackName, log),
                new DeployAwsCloudFormationConvention(
                    () => ClientHelpers.CreateCloudFormationClient(environment),
                    () => new EcsCloudFormationRequestBuilder(inputs.TemplateBody, inputs.Parameters, inputs.StackName, inputs.Tags, inputs.RoleArn),
                    new StackEventLogger(log),
                    _ => stackArn,
                    inputs.WaitForComplete,
                    inputs.StackName,
                    environment,
                    log,
                    inputs.WaitTimeout),
                new LogEcsTaskFailuresConvention(environment, inputs.ServiceName, inputs.ClusterName, log),
                new SetEcsOutputVariablesConvention(environment, inputs.StackName, inputs.ClusterName, inputs.ServiceName, log)
            ], log).RunConventions();

            return 0;
        }

        EcsCommandInputs ReadAndValidateInputs()
        {
            var templateBody = variables.Get(AwsSpecialVariables.CloudFormation.Template);
            Guard.NotNullOrWhiteSpace(templateBody, $"The CloudFormation template variable '{AwsSpecialVariables.CloudFormation.Template}' is not set.");

            var stackName = variables.Get(AwsSpecialVariables.CloudFormation.StackName);
            Guard.NotNullOrWhiteSpace(stackName, $"The CloudFormation stack name variable '{AwsSpecialVariables.CloudFormation.StackName}' is not set.");

            var clusterName = variables.Get(AwsSpecialVariables.Ecs.ClusterName);
            Guard.NotNullOrWhiteSpace(clusterName, $"The ECS cluster name variable '{AwsSpecialVariables.Ecs.ClusterName}' is not set.");

            var serviceName = variables.Get(AwsSpecialVariables.Ecs.ServiceName);
            Guard.NotNullOrWhiteSpace(serviceName, $"The ECS service name variable '{AwsSpecialVariables.Ecs.ServiceName}' is not set.");

            var parameters = JsonConvert.DeserializeObject<List<Parameter>>(
                variables.Get(AwsSpecialVariables.CloudFormation.TemplateParameters) ?? "[]") ?? [];

            var tags = JsonConvert.DeserializeObject<List<Tag>>(
                variables.Get(AwsSpecialVariables.CloudFormation.Tags) ?? "[]") ?? [];

            var roleArn = variables.Get(AwsSpecialVariables.CloudFormation.RoleArn);

            var waitOptionType = variables.Get(AwsSpecialVariables.Ecs.WaitOption.Type);
            Guard.NotNullOrWhiteSpace(waitOptionType, $"The wait option type variable '{AwsSpecialVariables.Ecs.WaitOption.Type}' is not set.");
            if (waitOptionType != "waitUntilCompleted" && waitOptionType != "waitWithTimeout" && waitOptionType != "dontWait")
                throw new CommandException($"The wait option type variable '{AwsSpecialVariables.Ecs.WaitOption.Type}' has an invalid value '{waitOptionType}'. Expected one of: 'waitUntilCompleted', 'waitWithTimeout', 'dontWait'.");

            var waitOptionTimeoutMs = variables.GetInt32(AwsSpecialVariables.Ecs.WaitOption.Timeout);
            if (waitOptionType == "waitWithTimeout" && !waitOptionTimeoutMs.HasValue)
                throw new CommandException($"Wait option '{AwsSpecialVariables.Ecs.WaitOption.Type}' is 'waitWithTimeout' but '{AwsSpecialVariables.Ecs.WaitOption.Timeout}' is not set.");

            return new EcsCommandInputs(
                TemplateBody: templateBody,
                StackName: stackName,
                ClusterName: clusterName,
                ServiceName: serviceName,
                Parameters: parameters,
                Tags: tags,
                RoleArn: roleArn,
                WaitForComplete: waitOptionType != "dontWait",
                WaitTimeout: waitOptionType == "waitWithTimeout" ? TimeSpan.FromMilliseconds(waitOptionTimeoutMs!.Value) : null);
        }

        record EcsCommandInputs(
            string TemplateBody,
            string StackName,
            string ClusterName,
            string ServiceName,
            List<Parameter> Parameters,
            List<Tag> Tags,
            string RoleArn,
            bool WaitForComplete,
            TimeSpan? WaitTimeout);
    }
}
