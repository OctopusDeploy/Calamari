using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.ECS.Model;
using Calamari.Common.Commands;

namespace Calamari.Aws.Integration.Ecs.Update;

public static class TaskDefinitionMutator
{
    public static TaskDefinition Apply(TaskDefinition template, IReadOnlyList<EcsContainerUpdate> updates)
    {
        var containers = template.ContainerDefinitions ?? [];
        var byName = containers.ToDictionary(c => c.Name, StringComparer.Ordinal);

        foreach (var u in updates)
        {
            if (!byName.TryGetValue(u.ContainerName, out var container))
            {
                throw new CommandException(
                    $"No matching container found for '{u.ContainerName}' in template task definition '{template.Family}'.");
            }

            if (!string.IsNullOrWhiteSpace(u.Image))
            {
                container.Image = u.Image;
            }

            ApplyEnvVars(container, u.EnvironmentVariables);
            ApplyEnvFiles(container, u.EnvironmentFiles);
        }

        return template;
    }

    static void ApplyEnvVars(ContainerDefinition container, EnvVarAction action)
    {
        if (action is null || action.Mode == EnvVarMode.None || action.Items.Count == 0)
        {
            return;
        }

        var plain = action.Items.Where(i => i.Kind == EnvVarKind.Plain)
                                .Select(i => new Amazon.ECS.Model.KeyValuePair { Name = i.Name, Value = i.Value })
                                .ToList();
        var secrets = action.Items.Where(i => i.Kind == EnvVarKind.Secret)
                                  .Select(i => new Secret { Name = i.Name, ValueFrom = i.Value })
                                  .ToList();

        if (action.Mode == EnvVarMode.Replace)
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

        if (container.Environment is { Count: 0 })
        {
            container.Environment = null;
        }
        if (container.Secrets is { Count: 0 })
        {
            container.Secrets = null;
        }
    }

    static void ApplyEnvFiles(ContainerDefinition container, EnvFileAction action)
    {
        if (action is null || action.Mode == EnvFileMode.None || action.Items.Count == 0)
        {
            return;
        }

        var files = action.Items.Select(i => new EnvironmentFile { Type = "s3", Value = i.Value }).ToList();

        if (action.Mode == EnvFileMode.Replace)
        {
            container.EnvironmentFiles = files;
        }
        else // Append
        {
            var existing = container.EnvironmentFiles ?? [];
            container.EnvironmentFiles = existing.Concat(files)
                                                 .GroupBy(f => f.Value, StringComparer.Ordinal)
                                                 .Select(g => g.Last())
                                                 .ToList();
        }

        if (container.EnvironmentFiles is { Count: 0 })
        {
            container.EnvironmentFiles = null;
        }
    }
}
