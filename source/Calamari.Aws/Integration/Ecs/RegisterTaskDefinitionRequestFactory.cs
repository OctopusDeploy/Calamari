using System.Collections.Generic;
using System.Linq;
using Amazon.ECS.Model;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using Octopus.Calamari.Contracts.Aws.Ecs;

namespace Calamari.Aws.Integration.Ecs;

public static class RegisterTaskDefinitionRequestFactory
{
    static readonly JsonSerializerSettings Settings = new() { NullValueHandling = NullValueHandling.Ignore };

    public static RegisterTaskDefinitionRequest FromTaskDefinition(
        TaskDefinition source,
        string targetFamily,
        IReadOnlyList<ContainerUpdate> containerUpdates,
        IReadOnlyList<KeyValuePair<string, string>> tags,
        IVariables variables)
    {
        // Serialize task definition to JSON and then deserialize back to request
        // This avoids us dropping any fields from task definition when performing our update
        var json = JsonConvert.SerializeObject(source, Settings);
        var request = JsonConvert.DeserializeObject<RegisterTaskDefinitionRequest>(json);
        request.Family = targetFamily;
        request.Tags = tags.Select(t => new Tag { Key = t.Key, Value = t.Value }).ToList();

        var containerLookup = (request.ContainerDefinitions ?? []).ToDictionary(c => c.Name);
        foreach (var update in containerUpdates)
        {
            if (!containerLookup.TryGetValue(update.ContainerName, out var containerToUpdate))
            {
                throw new CommandException($"No matching container found for '{update.ContainerName}' in template task definition '{source.Family}'.");
            }

            if (!string.IsNullOrWhiteSpace(update.PackageReference))
            {
                var image = variables.Get(PackageVariables.IndexedImage(update.PackageReference));
                if (!string.IsNullOrWhiteSpace(image))
                {
                    containerToUpdate.Image = image;
                }
            }

            ApplyEnvVars(containerToUpdate, update.EnvironmentVariables);
            ApplyEnvFiles(containerToUpdate, update.EnvironmentFiles);
        }

        return request;
    }

    static void ApplyEnvVars(ContainerDefinition container, EnvAction<TypedKeyValuePair> action)
    {
        if (action is null || action.Items.Count == 0)
        {
            return;
        }

        var plain = action.Items.Where(i => i.Type == KeyValueType.Plain)
                          .Select(i => new Amazon.ECS.Model.KeyValuePair { Name = i.Key, Value = i.Value })
                          .ToList();
        var secrets = action.Items.Where(i => i.Type == KeyValueType.Secret)
                            .Select(i => new Secret { Name = i.Key, ValueFrom = i.Value })
                            .ToList();

        if (action.Action == EnvActionMode.Replace)
        {
            container.Environment = plain;
            container.Secrets = secrets;
        }
        else // Append
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
        else // Append
        {
            var existing = container.EnvironmentFiles ?? [];
            container.EnvironmentFiles = existing.Concat(files)
                                                 .GroupBy(f => f.Value)
                                                 .Select(g => g.Last())
                                                 .ToList();
        }
    }
}
