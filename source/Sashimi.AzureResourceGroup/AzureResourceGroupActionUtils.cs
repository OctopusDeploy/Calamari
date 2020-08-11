using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.AzureResourceGroup
{
    static class AzureResourceGroupActionUtils
    {
        public static Dictionary<string, string?> ExtractParameterTypes(string template)
        {
            var jsonObject = JObject.Parse(template, new JsonLoadSettings { CommentHandling = CommentHandling.Ignore, LineInfoHandling = LineInfoHandling.Ignore });
            var parametersMetadata = new Dictionary<string, string?>();

            if (!jsonObject.TryGetValue("parameters", out var parameters))
            {
                return parametersMetadata;
            }

            foreach (var jToken in parameters)
            {
                if (!(jToken is JProperty parameter))
                {
                    continue;
                }

                var key = parameter.Name;
                var type = parameter.Value.SelectToken("type")?.Value<string>();

                parametersMetadata.Add(key, type);
            }

            return parametersMetadata;
        }

        public static string TemplateParameters(string parametersJson, IReadOnlyDictionary<string, string?> parameterMetadata, IImmutableVariableDictionary contextVariables)
        {
            using (var stringWriter = new StringWriter())
            using (JsonWriter writer = new JsonTextWriter(stringWriter))
            {
                writer.Formatting = Formatting.Indented;

                var jObject = JObject.Parse(parametersJson, new JsonLoadSettings { CommentHandling = CommentHandling.Ignore, LineInfoHandling = LineInfoHandling.Ignore });

                writer.WriteStartObject();

                foreach (var pair in jObject)
                {
                    var propertyName = pair.Key;
                    var selectToken = pair.Value?.SelectToken("value");
                    string? propertyValue;
                    if (selectToken is JValue jValue)
                    {
                        propertyValue = jValue.Value?.ToString();
                    }
                    else
                    {
                        propertyValue = selectToken?.ToString(Formatting.None);
                    }

                    var evaluatedValue = contextVariables.EvaluateIgnoringErrors(propertyValue);

                    writer.WritePropertyName(propertyName);
                    writer.WriteStartObject();
                    writer.WritePropertyName("value");
                    switch (parameterMetadata[propertyName]?.ToLower())
                    {
                        case "string":
                        case "securestring":
                            writer.WriteValue(evaluatedValue);
                            break;
                        case "int":
                            Int32.TryParse(evaluatedValue, out var intResult);
                            writer.WriteValue(intResult);
                            break;
                        case "bool":
                            Boolean.TryParse(evaluatedValue, out var boolResult);
                            writer.WriteValue(boolResult);
                            break;
                        default:
                            writer.WriteRawValue(evaluatedValue);
                            break;
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                writer.Flush();

                return stringWriter.ToString();
            }
        }
    }
}