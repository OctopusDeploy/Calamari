using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        readonly Regex octopusReservedVariablePattern = new Regex(@"^Octopus([^:]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string FileFormatName => "YAML";

        public bool TryModifyFile(string filePath, IVariables variables)
        {
            var variablesByKey = variables
                                 .Where(v => !octopusReservedVariablePattern.IsMatch(v.Key))
                                 .DistinctBy(v => v.Key)
                                 .ToDictionary<KeyValuePair<string, string>, string, Func<string>>(v => v.Key,
                                                                                                   v => () => variables.Get(v.Key),
                                                                                                   StringComparer.OrdinalIgnoreCase);

            // Read and transform the input file
            var outputEvents = new List<ParsingEvent>();
            (IYamlNode startEvent, string replacementValue)? structureWeAreReplacing = null;
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

                        var node = classifier.Process(ev);

                        if (structureWeAreReplacing == null)
                        {
                            // Not replacing: searching for things to replace, copying events to output.

                            if (node is YamlNode<Scalar> scalar
                                && variablesByKey.TryGetValue(scalar.Path, out var newValue))
                                outputEvents.Add(scalar.Event.ReplaceValue(newValue()));
                            else if (node is YamlNode<MappingStart> mappingStart
                                     && variablesByKey.TryGetValue(mappingStart.Path, out var mappingReplacement))
                                structureWeAreReplacing = (mappingStart, mappingReplacement());
                            else if (node is YamlNode<SequenceStart> sequenceStart
                                     && variablesByKey.TryGetValue(sequenceStart.Path, out var sequenceReplacement))
                                structureWeAreReplacing = (sequenceStart, sequenceReplacement());
                            else
                                outputEvents.Add(node.Event);
                        }
                        else
                        {
                            // Replacing: searching for the end of the structure we're replacing. No output until then.

                            if (node is YamlNode<MappingEnd>
                                && structureWeAreReplacing.Value.startEvent is YamlNode<MappingStart> mappingStart
                                && structureWeAreReplacing.Value.startEvent.Path == node.Path)
                            {
                                outputEvents.AddRange(ParseFragment(structureWeAreReplacing.Value.replacementValue,
                                                                    mappingStart.Event.Anchor,
                                                                    mappingStart.Event.Tag));
                                structureWeAreReplacing = null;
                            }
                            else if (node is YamlNode<SequenceEnd>
                                     && structureWeAreReplacing.Value.startEvent is YamlNode<SequenceStart> sequenceStart
                                     && structureWeAreReplacing.Value.startEvent.Path == node.Path)
                            {
                                outputEvents.AddRange(ParseFragment(structureWeAreReplacing.Value.replacementValue,
                                                                    sequenceStart.Event.Anchor,
                                                                    sequenceStart.Event.Tag));
                                structureWeAreReplacing = null;
                            }
                        }
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
                    emitter.Emit(outputEvent);

                writer.Close();
                outputText = writer.ToString();
            }

            File.WriteAllText(filePath, outputText);
            return true;
        }

        List<ParsingEvent> ParseFragment(string value, string? anchor, string? tag)
        {
            var result = new List<ParsingEvent>();
            try
            {
                using (var reader = new StringReader(value))
                {
                    var parser = new Parser(reader);
                    while (parser.MoveNext())
                    {
                        var ev = parser.Current;
                        if (ev != null && !(ev is StreamStart || ev is StreamEnd || ev is DocumentStart || ev is DocumentEnd))
                            result.Add(ev);
                    }
                }
            }
            catch
            {
                // The input could not be recognized as a structure. Falling back to treating it as a string.
                return new List<ParsingEvent>
                {
                    new Scalar(anchor,
                               tag,
                               value,
                               ScalarStyle.DoubleQuoted,
                               true,
                               true)
                };
            }

            return result;
        }
    }
}