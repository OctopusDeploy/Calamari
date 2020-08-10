using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Calamari.Common.Features.StructuredVariables
{
    public class YamlFormatVariableReplacer : IFileFormatVariableReplacer
    {
        static readonly Regex OctopusReservedVariablePattern = new Regex(@"^Octopus([^:]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        readonly ICalamariFileSystem fileSystem;

        public YamlFormatVariableReplacer(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public string FileFormatName => StructuredConfigVariablesFileFormats.Yaml;

        public bool IsBestReplacerForFileName(string fileName)
        {
            return fileName.EndsWith(".yml", StringComparison.InvariantCultureIgnoreCase)
                   || fileName.EndsWith(".yaml", StringComparison.InvariantCultureIgnoreCase);
        }

        public void ModifyFile(string filePath, IVariables variables)
        {
            try
            {
                var variablesByKey = variables
                                     .Where(v => !OctopusReservedVariablePattern.IsMatch(v.Key))
                                     .DistinctBy(v => v.Key)
                                     .ToDictionary<KeyValuePair<string, string>, string, Func<string>>(v => v.Key,
                                                                                                       v => () => variables.Get(v.Key),
                                                                                                       StringComparer.OrdinalIgnoreCase);

                // Read and transform the input file
                var fileText = fileSystem.ReadFile(filePath, out var encoding);
                var lineEnding = fileText.GetMostCommonLineEnding();

                var outputEvents = new List<ParsingEvent>();
                var indentDetector = new YamlIndentDetector();

                using (var reader = new StringReader(fileText))
                {
                    var scanner = new Scanner(reader, false);
                    var parser = new Parser(scanner);
                    var classifier = new YamlEventStreamClassifier();
                    (IYamlNode startEvent, string replacementValue)? structureWeAreReplacing = null;
                    while (parser.MoveNext())
                    {
                        var ev = parser.Current;
                        if (ev == null)
                            continue;

                        indentDetector.Process(ev);

                        if (ev is Comment c)
                            ev = c.RestoreLeadingSpaces();

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

                fileSystem.OverwriteFile(filePath,
                                                 writer =>
                                                 {
                                                     writer.NewLine = lineEnding == StringExtensions.LineEnding.Dos ? "\r\n" : "\n";
                                                     var emitter = new Emitter(writer, indentDetector.GetMostCommonIndent());
                                                     foreach (var outputEvent in outputEvents)
                                                         emitter.Emit(outputEvent);
                                                 },
                                                 encoding);
            }
            catch (Exception e) when (e is SyntaxErrorException || e is SemanticErrorException)
            {
                throw new StructuredConfigFileParseException(e.Message, e);
            }
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