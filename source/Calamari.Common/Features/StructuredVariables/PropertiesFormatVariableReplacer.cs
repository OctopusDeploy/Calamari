using System;
using System.Text.RegularExpressions;
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
        
        readonly Regex octopusReservedVariablePattern = new Regex(@"^Octopus([^:]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string FileFormatName => StructuredConfigVariablesFileFormats.Properties;

        public bool IsBestReplacerForFileName(string fileName)
        {
            return fileName.EndsWith(".properties", StringComparison.InvariantCultureIgnoreCase);
        }

        public void ModifyFile(string filePath, IVariables variables)
        {
            try
            {
                var (fileText, encoding) = EncodingDetectingFileReader.ReadToEnd(filePath);

                var parsed = Parser.Parse(fileText);
                var updated = parsed.Mutate(expr => TryReplaceValue(expr, variables));
                var serialized = updated.ToString();

                fileSystem.OverwriteFile(filePath, serialized, encoding);
            }
            catch (Sprache.ParseException e)
            {
                throw new StructuredConfigFileParseException(e.Message, e);
            }
        }

        ITopLevelExpression TryReplaceValue(ITopLevelExpression expr, IVariables variables)
        {
            switch (expr)
            {
                case KeyValuePairExpression pair:
                    var logicalName = pair?.Key?.Text?.LogicalValue ?? "";
                    if (!IsOctopusVariableName(logicalName) && variables.IsSet(logicalName))
                    {
                        var newEncodedValue = Encode.Value(variables.Get(logicalName));
                        var newValueExpr = new ValueExpression(new StringValue(newEncodedValue, newEncodedValue));
                        
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

        bool IsOctopusVariableName(string name)
        {
            return octopusReservedVariablePattern.IsMatch(name);
        }
    }
}