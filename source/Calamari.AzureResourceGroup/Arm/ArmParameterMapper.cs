using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.AzureResourceGroup.Arm;

public record ArmParameter(string Name, string Type);

public static class ArmParameterMapper
{
    
    public static List<ArmParameter> GetArmParameters(string armTemplateJson)
    {
        var template = JObject.Parse(armTemplateJson);

        return template["parameters"]
               ?.Children<JProperty>()
               .Select(p => new ArmParameter(
                                             Name: p.Name,
                                             Type: p.Value["type"]?.Value<string>() ?? "string"
                                            ))
               .ToList()
               ?? [];
    }


    public static string MatchParameters(string parametersString, List<ArmParameter> parameters, IVariables variables)
    {
        if (parameters.Count == 0 || string.IsNullOrEmpty(parametersString))
        {
            return string.Empty;
        }
        
        var parameterKeyValuePairs = JArray.Parse(parametersString)
                         .Select(item => new KeyValuePair<string, string>(
                                                                          item["Key"]!.Value<string>()!,
                                                                          item["Value"]!.Value<string>()!
                                                                         ))
                         .ToList();
        
        var result = new JObject();

        foreach (var kvp in parameterKeyValuePairs)
        {
            var armParameter = parameters.FirstOrDefault(p => p.Name == kvp.Key);
            if (armParameter != null)
            {
                var specifiedValue = GenerateValue(kvp, armParameter, variables);
                if (specifiedValue != null)
                {
                    result[kvp.Key] = new JObject { ["value"] = specifiedValue };
                }
            }
        }
        
        return result.ToString();
    }

    static JToken? GenerateValue(KeyValuePair<string, string> property, ArmParameter parameter, IVariables variables)
    {
        var evaluatedValue = variables.Evaluate(property.Value);

        JToken? valueToken = parameter.Type switch
                             {
                                 // NOTE: Array and Object are not defined here, but Server ignores them too
                                 "int" => int.TryParse(evaluatedValue, out var intResult) ? new JValue(intResult) : null,
                                 "bool" => bool.TryParse(evaluatedValue, out var boolResult) ? new JValue(boolResult) : null,
                                 "secureString" or "secureObject" => property.Value,
                                 _ => !string.IsNullOrEmpty(evaluatedValue) ? ParseStringOrObject(evaluatedValue) : null
                             };

        return valueToken;
    }
    
    // Used for handling Objects passed around as strings which
    // is part of what triggered https://slipway.octopushq.com/software-products/OctopusServer/problems/SoftwareReleaseProblems-9261
    static JToken ParseStringOrObject(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            try
            {
                var obj = JObject.Parse(trimmed);
                return new JValue(obj.ToString(Formatting.None));
            }
            catch (JsonException)
            {
                // not valid JSON, fall through to string
            }
        }

        return new JValue(value);
    }
}