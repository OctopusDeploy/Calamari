using System;
using System.Collections.Generic;
using Amazon.CloudFormation;
using Calamari.Aws.Deployment;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Aws.Integration.Ecs;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using Calamari.Deployment;
using Newtonsoft.Json;

namespace Calamari.Aws.Commands;

[Command("deploy-aws-ecs-service", Description = "Deploys a service to an Amazon ECS cluster")]
public class DeployEcsServiceCommand : Command
{
    readonly ILog log;
    readonly IVariables variables;
    readonly ICalamariFileSystem fileSystem;
    string templateFile;
    string templateParameterFile;

    public DeployEcsServiceCommand(ILog log, IVariables variables, ICalamariFileSystem fileSystem)
    {
        this.log = log;
        this.variables = variables;
        this.fileSystem = fileSystem;
        Options.Add("template=", "Path to the CloudFormation template file.", v => templateFile = v);
        Options.Add("templateParameters=", "Path to the CloudFormation template parameters JSON file.", v => templateParameterFile = v);
    }

    public override int Execute(string[] commandLineArguments)
    {
        Options.Parse(commandLineArguments);

        Guard.NotNullOrWhiteSpace(templateFile, "The --template argument is required.");

        var environment = AwsEnvironmentGeneration.Create(log, variables).GetAwaiter().GetResult();
        var inputs = ReadAndValidateInputs();

        var stackArn = new StackArn(inputs.StackName);
        var templateResolver = new TemplateResolver(fileSystem);

        new ConventionProcessor(new RunningDeployment(variables),
                                [
                                    new LogAwsUserInfoConvention(environment),
                                    new DeployAwsCloudFormationConvention(ClientFactory,
                                                                          TemplateFactory,
                                                                          new StackEventLogger(log),
                                                                          _ => stackArn,
                                                                          _ => null,
                                                                          inputs.WaitForComplete,
                                                                          inputs.StackName,
                                                                          environment,
                                                                          log,
                                                                          inputs.WaitTimeout),
                                    new SetEcsOutputVariablesConvention(environment,
                                                                        inputs.StackName,
                                                                        inputs.ClusterName,
                                                                        inputs.ServiceName,
                                                                        log)
                                ],
                                log).RunConventions();

        return 0;

        IAmazonCloudFormation ClientFactory() => ClientHelpers.CreateCloudFormationClient(environment);

        ICloudFormationRequestBuilder TemplateFactory() =>
            CloudFormationTemplate.Create(templateResolver,
                                          templateFile,
                                          templateParameterFile,
                                          filesInPackage: false,
                                          fileSystem,
                                          variables,
                                          inputs.StackName,
                                          capabilities: ["CAPABILITY_NAMED_IAM"],
                                          disableRollback: false,
                                          roleArn: null,
                                          tags: inputs.Tags,
                                          stackArn,
                                          ClientFactory);
    }

    EcsCommandInputs ReadAndValidateInputs()
    {
        var clusterName = variables.Get(AwsSpecialVariables.Ecs.ClusterName);
        Guard.NotNullOrWhiteSpace(clusterName, "Cluster name is required");

        var serviceName = variables.Get(AwsSpecialVariables.Ecs.ServiceName);
        Guard.NotNullOrWhiteSpace(serviceName, "Service name is required");

        var stackName = variables.Get(AwsSpecialVariables.CloudFormation.StackName);
        if (string.IsNullOrWhiteSpace(stackName))
        {
            stackName = EcsStackName.Generate(variables, clusterName, serviceName);
            log.Verbose($"No stack name supplied; generated \"{stackName}\".");
        }

        var userTags = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(variables.Get(AwsSpecialVariables.CloudFormation.Tags) ?? "[]") ?? [];
        var tags = EcsDefaultTags.Merge(variables, userTags);

        var waitOptionType = variables.Get(AwsSpecialVariables.Ecs.WaitOption.Type);
        Guard.NotNullOrWhiteSpace(waitOptionType, "The wait option is required");
        if (waitOptionType != "waitUntilCompleted" && waitOptionType != "waitWithTimeout" && waitOptionType != "dontWait")
        {
            throw new CommandException($"The wait option has an invalid value '{waitOptionType}'. Expected one of: 'waitUntilCompleted', 'waitWithTimeout', 'dontWait'.");
        }

        var waitOptionTimeoutMs = variables.GetInt32(AwsSpecialVariables.Ecs.WaitOption.Timeout);
        if (waitOptionType == "waitWithTimeout" && !waitOptionTimeoutMs.HasValue)
        {
            throw new CommandException("Wait option is 'waitWithTimeout' but timeout value is not set.");
        }

        return new EcsCommandInputs(
                                    StackName: stackName,
                                    ClusterName: clusterName,
                                    ServiceName: serviceName,
                                    Tags: tags,
                                    WaitForComplete: waitOptionType != "dontWait",
                                    WaitTimeout: waitOptionType == "waitWithTimeout" ? TimeSpan.FromMilliseconds(waitOptionTimeoutMs!.Value) : null);
    }

    record EcsCommandInputs(
        string StackName,
        string ClusterName,
        string ServiceName,
        List<KeyValuePair<string, string>> Tags,
        bool WaitForComplete,
        TimeSpan? WaitTimeout);
}
