#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Calamari.ArgoCD
{
    public class KustomizeImageReplacer : IContainerImageReplacer
    {
        const string ImagesNodeKey = "images";
        const string NewTagNodeKey = "newTag";
        const string NewNameNodeKey = "newName";
        const string NameNodeKey = "name";

        readonly string yamlContent;
        readonly string defaultRegistry;
        readonly ILog log;

        public KustomizeImageReplacer(string yamlContent, string defaultRegistry, ILog log)
        {
            this.yamlContent = yamlContent;
            this.defaultRegistry = defaultRegistry;
            this.log = log;
        }

        ImageReplacementResult NoChangeResult => new ImageReplacementResult(yamlContent, new HashSet<string>());

        public ImageReplacementResult UpdateImages(List<ContainerImageReference> imagesToUpdate)
        {
            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                log.Warn("Kustomization file content is empty or whitespace only.");
                return NoChangeResult;
            }

            using var reader = new StringReader(yamlContent);
            var stream = new YamlStream();
            stream.Load(reader);

            //we expect this to only be a single document
            //if there are no documents, do nothing
            if (stream.Documents.Count != 1 || !(stream.Documents[0].RootNode is YamlMappingNode rootNode))
            {
                log.Warn("Kustomization file must contain exactly one YAML document with a mapping root node.");
                return NoChangeResult;
            }

            //kustomization yaml has the images node at the top level
            var (imageKey, imagesNode) = rootNode.FirstOrDefault(kvp => new YamlScalarNode(ImagesNodeKey).Equals(kvp.Key));
            if (!(imagesNode is YamlSequenceNode imagesSequenceNode) || imageKey is null)
            {
                log.Warn("No 'images' sequence found in kustomization file.");
                return NoChangeResult;
            }

            //store the indexes that represent the start and end of the images sequence block
            //These will be used to replace the whole section later
            var originalImagesSequenceStartIndex = (int)imageKey.Start.Index;
            var originalImagesSequenceEndIndex = (int)imagesSequenceNode.AllNodes.Last().End.Index;

            //we need to get the correct new line char for the file. If we _don't_ know for some reason, just use unix line endings
            var newLine = yamlContent.DetectLineEnding() ?? "\n";

            // Determine if this is an indented sequence.
            // This is so when we reserialize we keep the same indenting
            // We do this by checking the previous two chars before the sequence start
            // If it's two spaces, then we assume it was indented
            var sequenceStartIndex = (int)imagesSequenceNode.Start.Index;
            var isIndentedSequence = yamlContent[sequenceStartIndex - 1] == ' ' && yamlContent[sequenceStartIndex - 2] == ' ';

            //each image node is a mapping node
            var replacementsMade = new HashSet<string>();
            foreach (var imageNode in imagesSequenceNode.OfType<YamlMappingNode>())
            {
                var matchedUpdate = GetMatchedContainerToUpdate(imagesToUpdate, imageNode);
                //no match, nothing to do
                if (matchedUpdate is null)
                {
                    continue;
                }

                //update or insert the newTag node
                var newTagNode = imageNode.GetChildNodeIfExists<YamlScalarNode>(NewTagNodeKey);
                if (newTagNode != null)
                {
                    if (!matchedUpdate.Comparison.TagMatch)
                    {
                        newTagNode.Value = matchedUpdate.Reference.Tag;
                        if (newTagNode.Style != ScalarStyle.SingleQuoted && newTagNode.Style != ScalarStyle.DoubleQuoted)
                        {
                            newTagNode.Style = ScalarStyle.DoubleQuoted;
                        }
                        replacementsMade.Add($"{matchedUpdate.Reference.ImageName}:{matchedUpdate.Reference.Tag}");
                    }
                }
                else
                {
                    imageNode.Children.Add(new YamlScalarNode(NewTagNodeKey), new YamlScalarNode(matchedUpdate.Reference.Tag) { Style = ScalarStyle.DoubleQuoted });
                    replacementsMade.Add($"{matchedUpdate.Reference.ImageName}:{matchedUpdate.Reference.Tag}");
                }

                //remove any digest node (as we want the newTag node to dictate the container version)
                imageNode.Children.Remove("digest");
            }

            //no changes made, return no change result
            if (replacementsMade.Count == 0)
            {
                return NoChangeResult;
            }

            var modifiedYaml = UpdateYamlWithUpdatedNode(isIndentedSequence,
                                                         newLine,
                                                         imageKey,
                                                         imagesSequenceNode,
                                                         originalImagesSequenceEndIndex,
                                                         originalImagesSequenceStartIndex);

            return new ImageReplacementResult(modifiedYaml, replacementsMade);
        }

        ImageReferenceMatch? GetMatchedContainerToUpdate(List<ContainerImageReference> imagesToUpdate, YamlMappingNode imageNode)
        {
            var nameNode = imageNode.GetChildNode<YamlScalarNode>(NameNodeKey);
            var name = nameNode.Value;
            if (name is null)
            {
                return null;
            }

            var newNameNode = imageNode.GetChildNodeIfExists<YamlScalarNode>(NewNameNodeKey);

            //if the newName node exists, we use that value as the container name, rather than the name node
            var testName = newNameNode?.Value ?? name;

            var currentReference = ContainerImageReference.FromReferenceString(testName, defaultRegistry);
            
            return imagesToUpdate.Select(i => new ImageReferenceMatch(i, i.CompareWith(currentReference)))
                                              .FirstOrDefault(i => i.Comparison.MatchesImage());
        }

        string UpdateYamlWithUpdatedNode(bool isIndentedSequence,
                                         string newLine,
                                         YamlNode imageKey,
                                         YamlSequenceNode imagesSequenceNode,
                                         int originalImagesSequenceEndIndex,
                                         int originalImagesSequenceStartIndex)
        {
            var builder = new SerializerBuilder();
            if (isIndentedSequence)
            {
                builder.WithIndentedSequences();
            }

            var serializer = builder.WithNewLine(newLine).Build();

            //create an in-memory new images node for serialization
            var newImagesNode = new YamlMappingNode(imageKey, imagesSequenceNode);

            var updatedImagesYaml = serializer.Serialize(newImagesNode);
            //the yaml serializer adds a newline at the end which we don't want to include, so kill it
            updatedImagesYaml = updatedImagesYaml.TrimEnd(newLine.ToCharArray());

            //remove the previous sequence block and insert the newly serialized one
            var lengthToRemove = originalImagesSequenceEndIndex - originalImagesSequenceStartIndex;

            return yamlContent
                   .Remove(originalImagesSequenceStartIndex, lengthToRemove)
                   .Insert(originalImagesSequenceStartIndex, updatedImagesYaml);
        }
        
        record ImageReferenceMatch(ContainerImageReference Reference, ContainerImageComparison Comparison);
    }
}

