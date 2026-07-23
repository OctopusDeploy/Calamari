using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.CloudFormation.Model;

namespace Calamari.Aws.Integration.CloudFormation.Templates
{
    public static class CloudFormationParameterMerge
    {
        public static List<Parameter> Merge(IEnumerable<Parameter> primary, IEnumerable<Parameter> overrides)
        {
            primary ??= Enumerable.Empty<Parameter>();
            overrides ??= Enumerable.Empty<Parameter>();

            var merged = new Dictionary<string, Parameter>();

            foreach (var parameter in primary)
            {
                if (string.IsNullOrWhiteSpace(parameter?.ParameterKey))
                    throw new ArgumentException("CloudFormation parameter key must not be null or empty.", nameof(primary));

                merged[parameter.ParameterKey] = parameter;
            }

            foreach (var parameter in overrides)
            {
                if (string.IsNullOrWhiteSpace(parameter?.ParameterKey))
                    throw new ArgumentException("CloudFormation parameter key must not be null or empty.", nameof(overrides));

                merged[parameter.ParameterKey] = parameter;
            }

            return merged.Values.ToList();
        }
    }
}
