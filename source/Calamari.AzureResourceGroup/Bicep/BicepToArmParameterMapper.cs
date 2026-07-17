using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.AzureResourceGroup.Bicep;

public static class BicepToArmParameterMapper
{
    record ArmParameter(string Name, string Type);
    
    public static string Map(string bicepParametersString, string armTemplateJson, IVariables variables)
    {
        var armParameters = JObject.Parse(armTemplateJson)["parameters"]?.Children<JProperty>()
                                   .Select(p => new ArmParameter(
                                                                 Name: p.Name,
                                                                 Type: p.Value["type"]?.Value<string>() ?? "string"
                                                                ))
                                   .ToList()
                            ?? [];
        
        if (armParameters.Count == 0 || string.IsNullOrEmpty(bicepParametersString))
        {
            return string.Empty;
        }

        var parameterKeyValuePairs = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(bicepParametersString) ?? [];
        
        var result = new JObject();

        var matched = parameterKeyValuePairs.Join(
                                                  armParameters,
                                                  kvp => kvp.Key,
                                                  p => p.Name,
                                                  (kvp, armParameter) => new { kvp, armParameter }
                                                 );

        foreach (var match in matched)
        {
            var specifiedValue = GenerateValue(match.kvp, match.armParameter, variables);
            if (specifiedValue != null)
            {
                result[match.kvp.Key] = new JObject { ["value"] = specifiedValue };
            }
        }

        
        return result.ToString();
    }

    static JToken? GenerateValue(KeyValuePair<string, string> property, ArmParameter parameter, IVariables variables)
    {
        var evaluatedValue = variables.Evaluate(property.Value);

        var valueToken = parameter.Type switch
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