using System;
using System.Collections.Generic;
using Calamari.Aws.Deployment;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration.Ecs;
using Calamari.CloudAccounts;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Newtonsoft.Json;
using Octopus.Calamari.Contracts.Aws.Ecs;
using Octopus.Calamari.Contracts.Serialization;

namespace Calamari.Aws.Commands;

[Command("update-aws-ecs-service", Description = "Updates an ECS service to a new task definition revision")]
public class UpdateEcsServiceCommand : Command
{
    readonly ILog log;
    readonly IVariables variables;

    public UpdateEcsServiceCommand(ILog log, IVariables variables)
    {
        this.log = log;
        this.variables = variables;
    }

    public override int Execute(string[] commandLineArguments)
    {
        var inputs = ReadAndValidateInputs();
        var environment = AwsEnvironmentGeneration.Create(log, variables).GetAwaiter().GetResult();

        using var ecsClient = EcsClientFactory.Create(environment);

        new ConventionProcessor(new RunningDeployment(variables),
                                [
                                    new LogAwsUserInfoConvention(environment),
                                    new UpdateEcsServiceConvention(
                                        ecsClient,
                                        log,
                                        environment,
                                        inputs.ClusterName,
                                        inputs.ServiceName,
                                        inputs.TemplateTaskDefinitionName,
                                        inputs.TargetTaskDefinitionName,
                                        inputs.Containers,
                                        inputs.Tags,
                                        inputs.WaitOption)
                                ],
                                log).RunConventions();

        return 0;
    }

    EcsUpdateServiceInputs ReadAndValidateInputs()
    {
        var settings = CalamariContractsSerializerSettings.Create();

        var clusterName = variables.Get(AwsSpecialVariables.Ecs.ClusterName);
        Guard.NotNullOrWhiteSpace(clusterName, "Cluster name is required");

        var serviceName = variables.Get(AwsSpecialVariables.Ecs.ServiceName);
        Guard.NotNullOrWhiteSpace(serviceName, "Service name is required");

        var targetFamily = variables.Get(AwsSpecialVariables.Ecs.TargetTaskDefinitionName);
        Guard.NotNullOrWhiteSpace(targetFamily, "Target task definition name is required");

        var templateFamily = variables.Get(AwsSpecialVariables.Ecs.TemplateTaskDefinitionName);
        if (string.IsNullOrWhiteSpace(templateFamily))
        {
            templateFamily = targetFamily;
        }

        var containersJson = variables.Get(AwsSpecialVariables.Ecs.Containers) ?? "[]";
        var containers = JsonConvert.DeserializeObject<List<EcsContainerUpdate>>(containersJson, settings) ?? [];
        if (containers.Count == 0)
        {
            throw new CommandException("At least one container is required.");
        }

        foreach (var c in containers)
        {
            Guard.NotNullOrWhiteSpace(c.ContainerName, "Container name is required");
        }

        var tagsJson = variables.Get(AwsSpecialVariables.CloudFormation.Tags) ?? "[]";
        var userTags = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(tagsJson) ?? [];
        var seenTagKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in userTags)
        {
            if (!seenTagKeys.Add(tag.Key))
            {
                throw new CommandException($"Duplicate tag key '{tag.Key}' in inputs.");
            }
        }

        var waitOption = JsonConvert.DeserializeObject<WaitOption>(variables.Get(AwsSpecialVariables.Ecs.Wait), settings);

        return new EcsUpdateServiceInputs(
                                          clusterName,
                                          serviceName,
                                          targetFamily,
                                          templateFamily,
                                          containers,
                                          userTags,
                                          waitOption);
    }
}

public record EcsUpdateServiceInputs(
    string ClusterName,
    string ServiceName,
    string TargetTaskDefinitionName,
    string TemplateTaskDefinitionName,
    List<EcsContainerUpdate> Containers,
    List<KeyValuePair<string, string>> Tags,
    WaitOption WaitOption);
