using System.Collections.Generic;
using System.Linq;
using Amazon.CDK;

namespace Calamari.Aws.Inputs.Ecs;

public static class TagExtensions
{
    public static ICfnTag[] ToCloudFormationTags(this IEnumerable<KeyValuePair<string, string>> tags)
    {
        return tags.Select(t => new CfnTag
            {
                Key = t.Key,
                Value = t.Value
            })
            .ToArray<ICfnTag>();
    }
}