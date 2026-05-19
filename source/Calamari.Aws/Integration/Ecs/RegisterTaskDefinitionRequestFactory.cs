using System.Collections.Generic;
using System.Linq;
using Amazon.ECS.Model;
using Calamari.Common.Commands;
using Newtonsoft.Json;
using Octopus.Calamari.Contracts.Aws.Ecs;

namespace Calamari.Aws.Integration.Ecs;

public static class RegisterTaskDefinitionRequestFactory
{
    static readonly JsonSerializerSettings Settings = new() { NullValueHandling = NullValueHandling.Ignore };

    public static RegisterTaskDefinitionRequest FromTaskDefinition(
        TaskDefinition source,
        string targetFamily,
        IReadOnlyList<EcsContainerUpdate> containerUpdates,
        IReadOnlyList<KeyValuePair<string, string>> tags)
    {
        // Serialize task definition to JSON and then deserialize back to request
        // This avoids us dropping any fields from task definition when performing our update
        var json = JsonConvert.SerializeObject(source, Settings);
        var request = JsonConvert.DeserializeObject<RegisterTaskDefinitionRequest>(json);
        request.Family = targetFamily;
        request.Tags = tags.Select(t => new Tag { Key = t.Key, Value = t.Value }).ToList();

        var containerLookup = (request.ContainerDefinitions ?? []).ToDictionary(c => c.Name);
        foreach (var containerUpdate in containerUpdates)
        {
            if (!containerLookup.TryGetValue(containerUpdate.ContainerName, out var containerToUpdate))
            {
                throw new CommandException($"No matching container found for '{containerUpdate.ContainerName}' in template task definition '{source.Family}'.");
            }

            if (!string.IsNullOrWhiteSpace(containerUpdate.Image))
            {
                containerToUpdate.Image = containerUpdate.Image;
            }

            ApplyEnvVars(containerToUpdate, containerUpdate.EnvironmentVariables);
            ApplyEnvFiles(containerToUpdate, containerUpdate.EnvironmentFiles);
        }

        return request;
    }

    static void ApplyEnvVars(ContainerDefinition container, EnvAction<EnvVarItem> action)
    {
        if (action is null || action.Items.Count == 0)
        {
            return;
        }

        var plain = action.Items.Where(i => i.Type == EnvVarType.Plain)
                          .Select(i => new Amazon.ECS.Model.KeyValuePair { Name = i.Key, Value = i.Value })
                          .ToList();
        var secrets = action.Items.Where(i => i.Type == EnvVarType.Secret)
                            .Select(i => new Secret { Name = i.Key, ValueFrom = i.Value })
                            .ToList();

        if (action.Action == EnvActionMode.Replace)
        {
            container.Environment = plain;
            container.Secrets = secrets;
        }
        else // Merge
        {
            var existingEnv = container.Environment ?? [];
            container.Environment = existingEnv
                                    .Where(e => plain.All(p => p.Name != e.Name))
                                    .Concat(plain)
                                    .ToList();

            var existingSecrets = container.Secrets ?? [];
            container.Secrets = existingSecrets
                                .Where(s => secrets.All(ns => ns.Name != s.Name))
                                .Concat(secrets)
                                .ToList();
        }
    }

    static void ApplyEnvFiles(ContainerDefinition container, EnvAction<string> action)
    {
        if (action is null || action.Items.Count == 0)
        {
            return;
        }

        var files = action.Items.Select(s => new EnvironmentFile { Type = "s3", Value = s }).ToList();

        if (action.Action == EnvActionMode.Replace)
        {
            container.EnvironmentFiles = files;
        }
        else // Merge
        {
            var existing = container.EnvironmentFiles ?? [];
            container.EnvironmentFiles = existing.Concat(files)
                                                 .GroupBy(f => f.Value)
                                                 .Select(g => g.Last())
                                                 .ToList();
        }
    }
}
