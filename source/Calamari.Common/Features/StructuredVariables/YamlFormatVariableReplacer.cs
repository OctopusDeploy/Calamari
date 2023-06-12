using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Calamari.Common.Features.StructuredVariables
{
    public class YamlFormatVariableReplacer : IFileFormatVariableReplacer
    {
        static readonly Regex OctopusReservedVariablePattern = new Regex(@"^Octopus([^:]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        
        private int replaced = 0;

        public YamlFormatVariableReplacer(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
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
                var variablesByKey = GetMappings(variables, new Dictionary<string, string>());

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
                    (IYamlNode startEvent, string? replacementValue)? structureWeAreReplacing = null;
                    var atDocumentStart = false;
                    
                    while (parser.MoveNext())
                    {
                        var ev = parser.Current;

                        if (ev is Comment c)
                        {
                            if (atDocumentStart)
                            {
                                var varName = GetCommentVariable(c.Value);
                                if (!string.IsNullOrWhiteSpace(varName))
                                {
                                    var patchVariables = GetPatchVariables(variables, varName);
                                    variablesByKey = GetMappings(variables, patchVariables);
                                }
                            }
                            ev = c.RestoreLeadingSpaces();
                        }

                        if (ev is DocumentStart)
                        {
                            atDocumentStart = true;
                        }
                        else
                        {
                            atDocumentStart = false;
                        }

                        if (ev is DocumentEnd)
                        {
                            variablesByKey = GetMappings(variables, new Dictionary<string, string>());
                        }
                        
                        if (ev == null)
                            continue;

                        indentDetector.Process(ev);

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
                            else if (node is YamlNode<Comment> comment)
                                structureWeAreReplacing = (comment, null);
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
                            else if ((node is YamlNode<MappingStart> || node is YamlNode<SequenceStart>)
                                     && structureWeAreReplacing.Value.startEvent is YamlNode<Comment>)
                            {
                                // We aren't doing any replacement here, YamlDotNet gives us the comment and the
                                // mapping/sequence start element in a different order to what we would expect
                                // (comment first, start element second instead of the other way around),
                                // so we are flipping them back to the other way around so the output is correct.
                                outputEvents.Add(node.Event);
                                outputEvents.Add(structureWeAreReplacing.Value.startEvent.Event);
                                structureWeAreReplacing = null;
                            }
                            else if (structureWeAreReplacing.Value.startEvent is YamlNode<Comment>)
                            {
                                // Comment after any other type of element, just put in the order in which they were given to us
                                outputEvents.Add(structureWeAreReplacing.Value.startEvent.Event);
                                outputEvents.Add(node.Event);
                                structureWeAreReplacing = null;
                            }
                        }
                    }
                    if (replaced == 0)
                        log.Info(StructuredConfigMessages.NoStructuresFound);
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

        string GetCommentVariable(string comment)
        {
            if (comment.TrimStart().StartsWith("patch:"))
            {
                return comment.Replace("patch:", "").Trim();
            }

            return string.Empty;
        }

        Dictionary<string, string> GetPatchVariables(IVariables variables, string variableName)
        {
            var patch = variables.Get(variableName);
            if (string.IsNullOrEmpty(patch))
            {
                return new Dictionary<string, string>();
            }
            
            var deserializer = new Deserializer();
            var yamlObject = deserializer.Deserialize(new StringReader(patch)) ?? new object();
            
            return new FlattenedProperties(yamlObject);
        }

        Dictionary<string, Func<string?>> GetMappings(
            IVariables variables,
            Dictionary<string, string> patchVariables)
        {
            var keys = variables.Select(v => v.Key).ToList();
            keys.AddRange(patchVariables.Keys.ToList());
            keys = keys.Distinct().Where(k => !OctopusReservedVariablePattern.IsMatch(k)).ToList();

            var merged = variables.Clone();
            foreach (var (k, v) in patchVariables)
            {
                merged.Add(k, v);
            }
            
            return keys
                .ToDictionary<string, string, Func<string?>>(k => k,
                    k => () =>
                    {
                        LogReplacement(k);
                        replaced++;
                        return merged.Get(k);
                    },
                    StringComparer.OrdinalIgnoreCase);
        }

        void LogReplacement(string key) => log.Verbose(StructuredConfigMessages.StructureFound(key));
        
        List<ParsingEvent> ParseFragment(string? value, string? anchor, string? tag)
        {
            var result = new List<ParsingEvent>();
            try
            {
                using (var reader = new StringReader(value))
                {
                    var parser = new Parser(reader);
                    bool added = false;
                    while (parser.MoveNext())
                    {
                        var ev = parser.Current;
                        if (ev != null && !(ev is StreamStart || ev is StreamEnd || ev is DocumentStart || ev is DocumentEnd))
                        {
                            result.Add(ev);
                            added = true;
                        }
                    }
                    if (!added)
                        throw new Exception("No content found in fragment");
                }
            }
            catch
            {
                // The input could not be recognized as a structure. Falling back to treating it as a string.
                result.Add(value != null
                               ? new Scalar(anchor,
                                            tag,
                                            value,
                                            ScalarStyle.DoubleQuoted,
                                            true,
                                            true)
                               : new Scalar(anchor,
                                            tag,
                                            "null",
                                            ScalarStyle.Plain,
                                            true,
                                            false));
            }

            return result;
        }
    }
}