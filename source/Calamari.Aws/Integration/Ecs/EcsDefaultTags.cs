using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Amazon.ECS.Model;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Aws.Integration.Ecs;

/// <summary>
/// Sanitize and merge tags used in ECS steps to match SPF
/// Deploy step does no deduplication but the update step does
/// </summary>
public static partial class EcsDefaultTags
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

    public static List<KeyValuePair<string, string>> Merge(
        IVariables variables,
        IEnumerable<KeyValuePair<string, string>> userTags) =>
        Sanitize(GetDefaults(variables)
                     .Concat(userTags?.Select(t => (t.Key, t.Value)) ?? []))
            .Select(t => new KeyValuePair<string, string>(t.Key, t.Value))
            .ToList();

    public static List<KeyValuePair<string, string>> MergeAndDeduplicateTags(
        IVariables variables,
        IEnumerable<KeyValuePair<string, string>> userTags,
        IEnumerable<Tag> existingTags) =>
        // Tag sources in priority order matching SPF
        // existing tags < Octopus defaults < user
        // GroupBy preserves ordering
        Sanitize((existingTags?.Select(t => (t.Key, t.Value)) ?? [])
                 .Concat(GetDefaults(variables))
                 .Concat(userTags?.Select(t => (t.Key, t.Value)) ?? [])
                 .GroupBy(t => t.Key)
                 .Select(g => g.Last()))
            .Select(t => new KeyValuePair<string, string>(t.Key, t.Value))
            .ToList();

    static IEnumerable<(string Key, string Value)> GetDefaults(IVariables variables) =>
        OctopusVariableNames
            .Select(n => (Key: n, Value: variables.Get(n)))
            .Where(p => !string.IsNullOrEmpty(p.Value));

    static IEnumerable<(string Key, string Value)> Sanitize(IEnumerable<(string Key, string Value)> tags) =>
        tags.Select(t => (
                        Key: Truncate(StripInvalidChars(t.Key), KeyMaxLength),
                        Value: Truncate(StripInvalidChars(t.Value), ValueMaxLength)))
            .Where(t => !string.IsNullOrEmpty(t.Key) && !string.IsNullOrEmpty(t.Value));

    static string StripInvalidChars(string v) =>
        string.IsNullOrEmpty(v) ? v : InvalidAwsCharsRegex().Replace(v, "").Trim();

    static string Truncate(string v, int max) =>
        string.IsNullOrEmpty(v) || v.Length <= max ? v : v[..max];

    [GeneratedRegex("[^a-zA-Z0-9 +=.:/@_-]")]
    private static partial Regex InvalidAwsCharsRegex();
}
