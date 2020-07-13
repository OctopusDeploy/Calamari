using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Calamari.Common.Features.StructuredVariables
{
    public interface IYamlFormatVariableReplacer : IFileFormatVariableReplacer
    {
    }

    public class YamlFormatVariableReplacer : IYamlFormatVariableReplacer
    {
        public string FileFormatName => "YAML";

        public bool TryModifyFile(string filePath, IVariables variables)
        {
            var variablesByKey = variables
                .DistinctBy(v => v.Key)
                .ToDictionary(v => v.Key, v => v.Value);

            // Read and transform the input file
            var outputEvents = new List<ParsingEvent>();
            try
            {
                using (var reader = new StreamReader(filePath))
                {
                    var parser = new Parser(reader);
                    var classifier = new YamlEventStreamClassifier();
                    while (parser.MoveNext())
                    {
                        var ev = parser.Current;
                        if (ev == null)
                            continue;

                        var found = classifier.Process(ev);
                        if (found is YamlScalarValueNode scalar
                            && variablesByKey.TryGetValue(scalar.Path, out string newValue))
                        {
                            ev = scalar.ReplaceValue(newValue);
                        }

                        outputEvents.Add(ev);
                    }
                }
            }
            catch (SyntaxErrorException)
            {
                // TODO: Report where the problem was in the input file
                return false;
            }

            // Write the replacement file
            string outputText;
            using (var writer = new StringWriter())
            {
                var emitter = new Emitter(writer);
                foreach (var outputEvent in outputEvents)
                {
                    emitter.Emit(outputEvent);
                }

                writer.Close();
                outputText = writer.ToString();
            }

            File.WriteAllText(filePath, outputText);
            return true;
        }
    }
}