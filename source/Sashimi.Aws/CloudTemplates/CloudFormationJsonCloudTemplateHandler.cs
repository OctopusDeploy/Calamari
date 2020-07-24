using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Octopus.Server.Extensibility.Metadata;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.CloudTemplates;

namespace Sashimi.Aws.CloudTemplates
{
    class CloudFormationJsonCloudTemplateHandler : ICloudTemplateHandler
    {
        readonly IFormatIdentifier formatIdentifier;

        public CloudFormationJsonCloudTemplateHandler(IFormatIdentifier formatIdentifier)
        {
            this.formatIdentifier = formatIdentifier;
        }

        public bool CanHandleTemplate(string providerId, string template)
            =>  AwsConstants.CloudTemplateProviderIds.Contains(providerId, StringComparer.OrdinalIgnoreCase) &&
                formatIdentifier.IsJson(template);

        public Metadata ParseTypes(string template)
        {
            if (template == null) return new Metadata();

            var hclElements = GetParameters(template);
            var properties = hclElements.Select(p => new PropertyMetadata
            {
                DisplayInfo = new DisplayInfo
                {
                    Description = p.Value.SelectToken("Description")?.ToString(),
                    Label = p.Key,
                    Required = true,
                    Options = GetOptions(p.Value)
                },
                Type = GetType(p.Value),
                Name = p.Key
            }).ToList();

            return new Metadata
            {
                Types = new List<TypeMetadata>
                {
                    new TypeMetadata
                    {
                        Name = AwsDataTypes.CloudFormationTemplateTypeName,
                        Properties = properties
                    }
                }
            };
        }

        public object ParseModel(string template)
        {
            var parameters = GetParameters(template);
            return parameters?.Select(x => new KeyValuePair<string, object>(x.Key.ToString(), GetDefaultValue(x.Value))).ToDictionary(x => x.Key, x => x.Value) ?? new Dictionary<string, object>();
        }

        object GetDefaultValue(JToken argValue)
        {
            var defaultValueToken = argValue.SelectToken("Default");
            return defaultValueToken?.ToString();
        }

        string GetType(JToken token)
        {
            return AwsDataTypes.MapToType(token.SelectToken("Type").ToString());
        }

        IDictionary<string, JToken> GetParameters(string template)
        {
            var o = JObject.Parse(template);
            IDictionary<string, JToken> parameters = (JObject)o["Parameters"];
            return parameters ?? new Dictionary<string, JToken>();
        }

        OptionsMetadata GetOptions(JToken value)
        {
            var values = value.SelectToken("AllowedValues");
            if (values == null)
            {
                return null;
            }

            OptionsMetadata optionsMeta = new OptionsMetadata();
            optionsMeta.SelectMode = "Single";
            optionsMeta.Values = values.ToObject<List<string>>().ToDictionary(x => x, x => x);
            return optionsMeta;
        }


    }
}
