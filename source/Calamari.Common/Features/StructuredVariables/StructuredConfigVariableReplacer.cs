using System;
using System.Linq;

namespace Calamari.Common.Features.StructuredVariables
{
    public class StructuredConfigVariableReplacer : IStructuredConfigVariableReplacer
    {
        readonly string featureToggleVariableName = "Octopus.Action.Package.StructuredConfigurationFeatureFlag";

        readonly IFileFormatVariableReplacer[] replacers;

        public StructuredConfigVariableReplacer(
            IJsonFormatVariableReplacer jsonReplacer,
            IYamlFormatVariableReplacer yamlReplacer
        )
        {
            // Order is important. YAML is a superset of JSON, so we want to try to parse
            // documents as JSON before we try YAML, to make sure we don't modify a JSON
            // file with YAML syntax.
            replacers = new IFileFormatVariableReplacer[]
            {
                jsonReplacer,
                yamlReplacer
            };
        }
        
        public void ModifyFile(string filePath, IVariables variables)
        {
            Log.Info($"Attempting to push variables into structured config file at '{filePath}'");

            // Toggle set of replacers based on feature flag
            IFileFormatVariableReplacer[] replacersToTry;
            if (variables.GetFlag(featureToggleVariableName))
            {
                Log.Info($"Feature toggle flag {featureToggleVariableName} detected. Trying replacers for all supported file formats.");
                replacersToTry = replacers;
            }
            else
            {
                replacersToTry = replacers.OfType<IJsonFormatVariableReplacer>().ToArray<IFileFormatVariableReplacer>();
            }

            foreach (var replacer in replacersToTry)
            {
                var fileUpdated = replacer.TryModifyFile(filePath, variables);
                if (fileUpdated)
                {
                    Log.Info($"The config file at '{filePath}' was treated as {replacer.FileFormatName} and has been updated.");
                    return;
                }
            }

            throw new Exception($"The config file at '{filePath}' couldn't be parsed.");
        }
    }
}
