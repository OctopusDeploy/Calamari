using System;
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
                .ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

            // Read and transform the input file
            var outputEvents = new List<ParsingEvent>();
            IYamlNode replacing = null;
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
                        ParsingEvent outputEvent;

                        var node = classifier.Process(ev);

                        if (replacing == null)
                        {
                            // Not replacing: searching for things to replace, copying events to output.

                            if (node is YamlNode<Scalar> scalar
                                && variablesByKey.TryGetValue(scalar.Path, out string newValue))
                            {
                                outputEvent = scalar.Event.ReplaceValue(newValue);
                            }
                            else if (node is YamlNode<MappingStart> mappingStart
                                     && variablesByKey.ContainsKey(mappingStart.Path))
                            {
                                replacing = mappingStart;
                                outputEvent = null;
                            }
                            else if (node is YamlNode<SequenceStart> sequenceStart
                                     && variablesByKey.ContainsKey(sequenceStart.Path))
                            {
                                replacing = sequenceStart;
                                outputEvent = null;
                            }
                            else
                            {
                                outputEvent = node.Event;
                            }
                        }
                        else
                        {
                            // Replacing: searching for the end of the structure we're replacing. No output until then.

                            if (replacing.Path == node.Path
                                && replacing is YamlNode<MappingStart> mappingStart
                                && node is YamlNode<MappingEnd> mappingEnd
                                && variablesByKey.TryGetValue(mappingEnd.Path, out string mappingReplacementValue))
                            {
                                outputEvent = new Scalar(
                                    mappingStart.Event.Anchor,
                                    mappingStart.Event.Tag,
                                    mappingReplacementValue,
                                    ScalarStyle.DoubleQuoted,
                                    true,
                                    true,
                                    mappingStart.Event.Start,
                                    mappingStart.Event.End);
                                replacing = null;
                            }
                            else if (replacing.Path == node.Path
                                     && replacing is YamlNode<SequenceStart> sequenceStart
                                     && node is YamlNode<SequenceEnd> sequenceEnd
                                     && variablesByKey.TryGetValue(sequenceEnd.Path,
                                         out string sequenceReplacementValue))
                            {
                                outputEvent = new Scalar(
                                    sequenceStart.Event.Anchor,
                                    sequenceStart.Event.Tag,
                                    sequenceReplacementValue,
                                    ScalarStyle.DoubleQuoted,
                                    true,
                                    true,
                                    sequenceStart.Event.Start,
                                    sequenceStart.Event.End);
                                replacing = null;
                            }
                            else
                            {
                                outputEvent = null;
                            }
                        }

                        if (outputEvent != null)
                            outputEvents.Add(outputEvent);
                    }
                }
            }
            catch (SyntaxErrorException)
            {
                // TODO ZDY: Report where the problem was in the input file
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