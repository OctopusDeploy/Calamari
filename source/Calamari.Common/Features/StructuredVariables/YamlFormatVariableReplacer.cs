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
                string updatedText;
                using (var writer = new StringWriter())
                using (var reader = new StreamReader(filePath))
                {
                    var parser = new Parser(reader);
                    var classifier = new YamlEventStreamClassifier();
                    var emitter = new Emitter(writer);
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

                        emitter.Emit(ev);
                    }

                    writer.Close();
                    updatedText = writer.ToString();
                }

                File.WriteAllText(filePath, updatedText);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}