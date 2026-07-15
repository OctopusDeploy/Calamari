using System.Collections.Generic;
using System.Linq;
using Amazon.CloudFormation.Model;

namespace Calamari.Aws.Integration.CloudFormation.Templates
{
    public static class CloudFormationParameterMerge
    {
        public static List<Parameter> Merge(IEnumerable<Parameter> primary, IEnumerable<Parameter> overrides)
        {
            var merged = new Dictionary<string, Parameter>();

            foreach (var parameter in primary)
                merged[parameter.ParameterKey] = parameter;

            foreach (var parameter in overrides)
                merged[parameter.ParameterKey] = parameter;

            return merged.Values.ToList();
        }
    }
}
