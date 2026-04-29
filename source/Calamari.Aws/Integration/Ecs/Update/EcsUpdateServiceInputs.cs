using System;
using System.Collections.Generic;
using Calamari.Aws.Deployment;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.Aws.Integration.Ecs.Update;

public record EcsUpdateServiceInputs(
    string ClusterName,
    string ServiceName,
    string TargetTaskDefinitionName,
    string TemplateTaskDefinitionName,
    IReadOnlyList<EcsContainerUpdate> Containers,
    IReadOnlyList<KeyValuePair<string, string>> UserTags,
    WaitOptionType WaitOption,
    TimeSpan? WaitTimeout)
{
    public static EcsUpdateServiceInputs Parse(IVariables variables)
    {
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
        var containers = JsonConvert.DeserializeObject<List<EcsContainerUpdate>>(containersJson) ?? [];
        if (containers.Count == 0)
        {
            throw new CommandException("At least one container is required.");
        }

        var seenContainerNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in containers)
        {
            Guard.NotNullOrWhiteSpace(c.ContainerName, "Container name is required");
            if (!seenContainerNames.Add(c.ContainerName))
            {
                throw new CommandException($"Duplicate container name '{c.ContainerName}' in inputs.");
            }
        }

        var tagsJson = variables.Get(AwsSpecialVariables.Ecs.Tags) ?? "[]";
        var userTags = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(tagsJson) ?? [];
        var seenTagKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in userTags)
        {
            if (!seenTagKeys.Add(tag.Key))
            {
                throw new CommandException($"Duplicate tag key '{tag.Key}' in inputs.");
            }
        }

        var waitOptionRaw = variables.Get(AwsSpecialVariables.Ecs.WaitOption.Type);
        Guard.NotNullOrWhiteSpace(waitOptionRaw, "The wait option is required");
        var waitOption = waitOptionRaw switch
        {
            "waitUntilCompleted" => WaitOptionType.WaitUntilCompleted,
            "waitWithTimeout"    => WaitOptionType.WaitWithTimeout,
            "dontWait"           => WaitOptionType.DontWait,
            _ => throw new CommandException(
                $"The wait option has an invalid value '{waitOptionRaw}'. Expected one of: 'waitUntilCompleted', 'waitWithTimeout', 'dontWait'.")
        };

        TimeSpan? timeout = null;
        var timeoutMs = variables.GetInt32(AwsSpecialVariables.Ecs.WaitOption.Timeout);
        if (waitOption == WaitOptionType.WaitWithTimeout)
        {
            if (!timeoutMs.HasValue)
            {
                throw new CommandException("Wait option is 'waitWithTimeout' but timeout value is not set.");
            }
            timeout = TimeSpan.FromMilliseconds(timeoutMs.Value);
        }

        return new EcsUpdateServiceInputs(
            clusterName,
            serviceName,
            targetFamily,
            templateFamily,
            containers,
            userTags,
            waitOption,
            timeout);
    }
}

public enum WaitOptionType
{
    WaitUntilCompleted,
    WaitWithTimeout,
    DontWait
}
