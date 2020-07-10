using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Integration.FileSystem;
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

            try
            {
                var outputEvents = new List<ParsingEvent>();
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
            catch
            {
                return false;
            }
        }
    }
}