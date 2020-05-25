using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Octopus.CoreUtilities.Extensions;
using Octopus.Server.Extensibility.Metadata;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.CloudTemplates;

namespace Sashimi.Terraform.CloudTemplates
{
    public class TerraformJsonCloudTemplateHandler : ICloudTemplateHandler
    {
        readonly IFormatIdentifier formatIdentifier;

        public TerraformJsonCloudTemplateHandler(IFormatIdentifier formatIdentifier)
        {
            this.formatIdentifier = formatIdentifier;
        }

        public bool CanHandleTemplate(string providerId, string template)
            => TerraformConstants.CloudTemplateProviderId.Equals(providerId, StringComparison.OrdinalIgnoreCase) &&
                formatIdentifier.IsJson(template);


        public Metadata ParseTypes(string template)
        {
            return template?
                .Map(GetVariables)
                .Map(variable => variable.Select(p => new PropertyMetadata
                    {
                        DisplayInfo = new DisplayInfo
                        {
                            Description = p.Value!.SelectToken("description")?.ToString(),
                            Label = p.Key,
                            Required = true,
                        },
                        Type = GetType(p.Value!),
                        Name = p.Key,
                    }).ToList()
                )
                .Map(properties => new List<TypeMetadata>
                {
                    new TypeMetadata
                    {
                        Name = TerraformDataTypes.TerraformTemplateTypeName,
                        Properties = properties
                    }
                })
                .Map(typeMetadata => new Metadata() {Types = typeMetadata}) ?? new Metadata();
        }

        public object ParseModel(string template)
        {
            var parameters = GetVariables(template);
            return parameters?
                .Select(x => new KeyValuePair<string, object?>(x.Key.ToString(), GetDefaultValue(x.Value!)))
                .ToDictionary(x => x.Key, x => x.Value) ?? new Dictionary<string, object?>();
        }

        object? GetDefaultValue(JToken argValue)
        {
            var defaultValueToken = argValue.SelectToken("default");
            return defaultValueToken?.ToString();
        }

        /// <summary>
        /// https://www.terraform.io/docs/configuration/variables.html
        /// Valid values are string, list, and map. If this field is omitted, the variable type will be inferred based on default.
        /// If no default is provided, the type is assumed to be string.
        /// </summary>
        string GetType(JToken token)
        {
            var type = token.SelectToken("type");
            if (type != null)
            {
                return TerraformDataTypes.MapToType(type.ToString());
            }

            // We can determine the type from the default value
            var defaultValue = token.SelectToken("default");
            if (defaultValue == null) return "string";
            switch (defaultValue.Type)
            {
                case JTokenType.Array:
                    return TerraformDataTypes.RawList;
                case JTokenType.Object:
                    return TerraformDataTypes.RawMap;
            }

            return "string";

            // Otherwise we default to a string
        }

        IDictionary<string, JToken?> GetVariables(string template)
        {
            var o = JObject.Parse(template);
            IDictionary<string, JToken?>? variables = (JObject) o["variable"]!;
            return variables ?? new Dictionary<string, JToken?>();
        }
    }
}