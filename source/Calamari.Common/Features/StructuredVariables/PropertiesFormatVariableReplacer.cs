using System;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using JavaPropertiesParser;
using JavaPropertiesParser.Expressions;
using JavaPropertiesParser.Utils;

namespace Calamari.Common.Features.StructuredVariables
{
    public class PropertiesFormatVariableReplacer : IFileFormatVariableReplacer
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public PropertiesFormatVariableReplacer(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public string FileFormatName => StructuredConfigVariablesFileFormats.Properties;

        public bool IsBestReplacerForFileName(string fileName)
        {
            return fileName.EndsWith(".properties", StringComparison.InvariantCultureIgnoreCase);
        }

        public void ModifyFile(string filePath, IVariables variables)
        {
            try
            {
                var fileText = fileSystem.ReadFile(filePath, out var encoding);

                var parsed = Parser.Parse(fileText);
                var replaced = 0;
                var updated = parsed.Mutate(expr =>
                                            {
                                                var newExpr = TryReplaceValue(expr, variables);
                                                if (!ReferenceEquals(newExpr, expr))
                                                    replaced++;
                                                return newExpr;
                                            });
                if (replaced == 0)
                    log.Info(StructuredConfigMessages.NoStructuresFound);
                var serialized = updated.ToString();

                fileSystem.OverwriteFile(filePath, serialized, encoding);
            }
            catch (Sprache.ParseException e)
            {
                throw new StructuredConfigFileParseException(e.Message, e);
            }
        }

        bool IsOctopusVariableName(string variableName)
        {
            return variableName.StartsWith("Octopus", StringComparison.OrdinalIgnoreCase);
        }

        ITopLevelExpression TryReplaceValue(ITopLevelExpression expr, IVariables variables)
        {
            switch (expr)
            {
                case KeyValuePairExpression pair:
                    var logicalName = pair.Key?.Text?.LogicalValue ?? "";
                    if (!IsOctopusVariableName(logicalName) && variables.IsSet(logicalName))
                    {
                        log.Verbose(StructuredConfigMessages.StructureFound(logicalName));
                        
                        var logicalValue = variables.Get(logicalName);
                        var encodedValue = Encode.Value(logicalValue);
                        var newValueExpr = new ValueExpression(new StringValue(logicalValue, encodedValue));
                        
                        // In cases where a key was specified with neither separator nor value
                        // we have to add a separator, otherwise the value becomes part of the key.
                        var separator = pair.Separator ?? new SeparatorExpression(":");
                        return new KeyValuePairExpression(pair.Key, separator, newValueExpr);
                    }
                    else
                    {
                        return expr;
                    }

                default:
                    return expr;
            }
        }
    }
}
