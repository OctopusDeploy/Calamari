using System.Collections.Generic;
using System.Linq;
using Cfn = Calamari.Aws.Integration.Ecs.Deploy.Cfn;

namespace Calamari.Aws.Inputs.Ecs;

public static class TagMappingExtensions
{
    // SPF always emits Tags as an array — empty becomes [] not omitted.
    public static Cfn.Tag[] ToCloudFormationTags(this IEnumerable<KeyValuePair<string, string>> tags) =>
        tags.Select(t => new Cfn.Tag { Key = t.Key, Value = t.Value }).ToArray();
}
