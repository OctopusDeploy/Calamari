using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Octopus.CoreParsers.Hcl;
using Octopus.Server.Extensibility.Metadata;
using Sashimi.Server.Contracts.Variables;
using Sashimi.Terraform.CloudTemplates;

namespace Sashimi.Terraform.ActionHandler
{
    static class TerraformVariableFileGenerator
    {
        /// <summary>
        /// When variables are supplied from the UI (i.e. not from a package), all values are strings.
        /// So maps and lists are just strings, even though they should be objects.
        ///
        /// This method find variables that should be objects, and parses the strings sent in
        /// into real objects.
        /// </summary>
        public static string ConvertStringPropsToObjects(
            TerraformTemplateFormat terraformTemplateFormat,
            IImmutableVariableDictionary variableDictionary,
            string variables,
            Metadata metadata)
        {
            var parsedProperties = JObject.Parse(variables);
            RemoveEmptyVariables(parsedProperties);

            switch (terraformTemplateFormat)
            {
                case TerraformTemplateFormat.Json:
                    return GenerateJsonVariables(parsedProperties, variableDictionary, metadata);
                case TerraformTemplateFormat.Hcl:
                    return GenerateHclVariables(parsedProperties, variableDictionary, metadata);
                default:
                    throw new ArgumentOutOfRangeException(nameof(terraformTemplateFormat), terraformTemplateFormat, null);
            }
        }

        /// <summary>
        /// Generate the HCL tfvars file
        /// </summary>
        static string GenerateHclVariables(JObject parsedProperties, IImmutableVariableDictionary variableDictionary, Metadata metadata)
        {
            var properties = GetPropertiesAsList(parsedProperties, metadata, GetHclVariableValue);
            var asStr = string.Join("\n", properties);
            return variableDictionary.EvaluateIgnoringErrors(asStr)!;
        }

        /// <summary>
        /// Generate the JSON tfvars.json file
        /// </summary>
        static string GenerateJsonVariables(JObject parsedProperties, IImmutableVariableDictionary variableDictionary, Metadata metadata)
        {
            var properties = GetPropertiesAsList(parsedProperties, metadata, GetJsonVariableValue);
            var asJson = "{" + string.Join(",", properties) + "}";
            return variableDictionary.EvaluateIgnoringErrors(asJson)!;
        }

        static List<string> GetPropertiesAsList(JObject parsedProperties, Metadata metadata, Func<string?, JProperty, string> getVariableValue)
        {
            var properties = parsedProperties
                             // get the populated properties
                             .Properties()
                             .ToList()
                             // convert each property to a string, list or map, preserving any variable substitutions
                             .Select(prop => getVariableValue(GetPropertyType(metadata, prop.Name), prop))
                             .ToList();
            return properties;
        }

        /// <summary>
        /// If the user has not submitted a value, don't save it in the variables file.
        /// This method will strip out any properties that don't have values.
        /// </summary>
        /// <param name="variables">The list of variables</param>
        /// <returns>A new JObject that contains only populated properties</returns>
        static void RemoveEmptyVariables(JObject variables)
        {
            var emptyProperties = variables
                                  .Properties()
                                  .Where(p => !p.HasValues || string.IsNullOrEmpty(p.Value.ToString()))
                                  .ToArray();
            foreach (var property in emptyProperties)
                variables.Remove(property.Name);
        }

        /// <summary>
        /// Get the value of the JSON list or map
        /// </summary>
        /// <param name="propertyType">The property type (map, list, string)</param>
        /// <param name="property">The string representation of the property entered by the user</param>
        /// <returns>The raw JSON property</returns>
        static string GetJsonVariableValue(string? propertyType, JProperty property)
        {
            if (propertyType != null && propertyType.StartsWith(TerraformDataTypes.RawPrefix))
                return "\"" + HclParser.EscapeString(property.Name) + "\": " + property.Value;

            return "\"" + HclParser.EscapeString(property.Name) + "\": \"" + HclParser.EscapeString(property.Value.ToString()) + "\"";
        }

        /// <summary>
        /// Get the value of the HCL list or map
        /// </summary>
        /// <param name="propertyType">The property type (map, list, string)</param>
        /// <param name="property">The string representation of the property entered by the user</param>
        /// <returns>The raw JSON property</returns>
        static string GetHclVariableValue(string? propertyType, JProperty property)
        {
            if (propertyType != null && propertyType.StartsWith(TerraformDataTypes.RawPrefix))
                return HclParser.EscapeString(property.Name) + " = " + property.Value;

            return HclParser.EscapeString(property.Name) + " = \"" + HclParser.EscapeString(property.Value.ToString()) + "\"";
        }

        public static string? GetPropertyType(Metadata metadata, string property)
        {
            return metadata.Types.FirstOrDefault(type => type.Name == TerraformDataTypes.TerraformTemplateTypeName)
                           ?.Properties
                           .FirstOrDefault(prop => prop.Name == property)
                           ?.Type;
        }
    }
}