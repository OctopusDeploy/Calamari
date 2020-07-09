using System;
using System.Collections.Generic;

namespace Calamari.Features.StructuredVariables
{
    public class StructuredConfigVariableReplacer : IStructuredConfigVariableReplacer
    {
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

            foreach (var replacer in replacers)
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
