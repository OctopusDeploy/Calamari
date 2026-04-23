using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Amazon.CloudFormation.Model;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Aws.Integration.Ecs;

// Merges 19 default Octopus tags with user tags, sanitises, and truncates to AWS limits (128/256).
// Mirrors SPF's generateDefaultOctopusTags + sanitizeAwsTagString.
public static partial class EcsTagBuilder
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

    public static List<Tag> Build(IVariables variables, IEnumerable<Tag> userTags)
    {
        var defaultTags = OctopusVariableNames
                          .Select(name => new Tag { Key = name, Value = variables.Get(name) })
                          .Where(p => !string.IsNullOrEmpty(p.Value));

        return defaultTags.Concat(userTags)
                          .Select(t => new Tag
                          {
                              Key = Truncate(SanitizeAwsTag(t.Key), KeyMaxLength),
                              Value = Truncate(SanitizeAwsTag(t.Value), ValueMaxLength)
                          })
                          .Where(t => !string.IsNullOrEmpty(t.Key) && !string.IsNullOrEmpty(t.Value))
                          .ToList();
    }

    static string SanitizeAwsTag(string value) =>
        string.IsNullOrEmpty(value) ? value : InvalidAwsCharsRegex().Replace(value, "").Trim();

    static string Truncate(string value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];

    [GeneratedRegex("[^a-zA-Z0-9 +=.:/@_-]")]
    private static partial Regex InvalidAwsCharsRegex();
}
