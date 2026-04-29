using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Aws.Integration.Ecs;

// Tag merge for the Update Service step. Defaults first, user wins on key collision.
// Mirrors SPF's appendKeyValuePairs(defaultTags, userTags) + sanitizeAwsTagString.
public static partial class EcsUpdateTags
{
    const int KeyMaxLength = 128;
    const int ValueMaxLength = 256;

    static readonly string[] OctopusVariableNames =
    [
        "Octopus.Project.Id",
        "Octopus.Action.Id",
        "Octopus.Deployment.Id",
        "Octopus.RunbookRun.Id",
        "Octopus.Step.Id",
        "Octopus.Environment.Id",
        "Octopus.Deployment.Tenant.Id",
        "Octopus.Deployment.Name",
        "Octopus.Deployment.Tenant.Name",
        "Octopus.Environment.Name",
        "Octopus.Project.Name",
        "Octopus.Release.Channel.Name",
        "Octopus.Space.Name",
        "Octopus.Action.Name",
        "Octopus.Step.Name",
        "Octopus.Runbook.Name",
        "Octopus.RunbookRun.Name",
        "Octopus.RunbookSnapshot.Name",
        "Octopus.Machine.Name"
    ];

    public static List<KeyValuePair<string, string>> Merge(IVariables variables, IEnumerable<KeyValuePair<string, string>> userTags)
    {
        var defaults = OctopusVariableNames
            .Select(n => new KeyValuePair<string, string>(n, variables.Get(n)))
            .Where(p => !string.IsNullOrEmpty(p.Value));

        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var t in defaults)
        {
            byKey[t.Key] = t.Value;
        }
        foreach (var t in userTags)
        {
            byKey[t.Key] = t.Value;
        }

        return byKey
               .Select(kv => new KeyValuePair<string, string>(
                   Truncate(Sanitize(kv.Key), KeyMaxLength),
                   Truncate(Sanitize(kv.Value), ValueMaxLength)))
               .Where(t => !string.IsNullOrEmpty(t.Key) && !string.IsNullOrEmpty(t.Value))
               .ToList();
    }

    static string Sanitize(string v) =>
        string.IsNullOrEmpty(v) ? v : InvalidCharsRegex().Replace(v, "").Trim();

    static string Truncate(string v, int max) =>
        string.IsNullOrEmpty(v) || v.Length <= max ? v : v[..max];

    [GeneratedRegex("[^a-zA-Z0-9 +=.:/@_-]")]
    private static partial Regex InvalidCharsRegex();
}
