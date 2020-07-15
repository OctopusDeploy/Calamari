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
            IYamlNode startOfStructureWeAreReplacing = null;
            string replacementValue = null;
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

                        if (startOfStructureWeAreReplacing == null)
                        {
                            // Not replacing: searching for things to replace, copying events to output.

                            if (node is YamlNode<Scalar> scalar
                                && variablesByKey.TryGetValue(scalar.Path, out string newValue))
                            {
                                outputEvent = scalar.Event.ReplaceValue(newValue);
                            }
                            else if (node is YamlNode<MappingStart> mappingStart
                                     && variablesByKey.TryGetValue(mappingStart.Path, out replacementValue))
                            {
                                startOfStructureWeAreReplacing = mappingStart;
                                outputEvent = null;
                            }
                            else if (node is YamlNode<SequenceStart> sequenceStart
                                     && variablesByKey.TryGetValue(sequenceStart.Path, out replacementValue))
                            {
                                startOfStructureWeAreReplacing = sequenceStart;
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

                            if (node is YamlNode<MappingEnd> mappingEnd
                                && startOfStructureWeAreReplacing is YamlNode<MappingStart> mappingStart
                                && startOfStructureWeAreReplacing.Path == node.Path)
                            {
                                outputEvent = new Scalar(
                                    mappingStart.Event.Anchor,
                                    mappingStart.Event.Tag,
                                    replacementValue,
                                    ScalarStyle.DoubleQuoted,
                                    isPlainImplicit: true,
                                    isQuotedImplicit: true,
                                    mappingStart.Event.Start,
                                    mappingStart.Event.End);
                                startOfStructureWeAreReplacing = null;
                            }
                            else if (node is YamlNode<SequenceEnd> sequenceEnd
                                     && startOfStructureWeAreReplacing is YamlNode<SequenceStart> sequenceStart
                                     && startOfStructureWeAreReplacing.Path == node.Path)
                            {
                                outputEvent = new Scalar(
                                    sequenceStart.Event.Anchor,
                                    sequenceStart.Event.Tag,
                                    replacementValue,
                                    ScalarStyle.DoubleQuoted,
                                    isPlainImplicit: true,
                                    isQuotedImplicit: true,
                                    sequenceStart.Event.Start,
                                    sequenceStart.Event.End);
                                startOfStructureWeAreReplacing = null;
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