using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using Octopus.CoreUtilities.Extensions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public class InlineJson6902ImageReplacer : IContainerImageReplacer
{
    readonly string input;
    readonly string defaultRegistry;
    readonly ILog log;

    public InlineJson6902ImageReplacer(string input, string defaultRegistry, ILog log)
    {
        this.input = input;
        this.defaultRegistry = defaultRegistry;
        this.log = log;
    }

    public ImageReplacementResult UpdateImages(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
            var yamlStream = YamlStreamLoader.TryLoad(input, log, "inline JSON 6902 patches");
            if (yamlStream?.Documents.Count != 1 || !(yamlStream.Documents[0].RootNode is YamlMappingNode rootNode))
            {
                return new ImageReplacementResult(input, new HashSet<string>(), new HashSet<string>());
            }

            if (!rootNode.Children.TryGetValue(new YamlScalarNode("patchesJson6902"), out var patchesNode) || !(patchesNode is YamlSequenceNode patchSequence))
            {
                return new ImageReplacementResult(input, new HashSet<string>(), new HashSet<string>());
            }

            var allUpdatedImages = new HashSet<string>();

            foreach (var patchEntryNode in patchSequence.Children.OfType<YamlMappingNode>())
            {
                if (patchEntryNode.Children.TryGetValue(new YamlScalarNode("patch"), out var patchContentNode) && patchContentNode is YamlScalarNode patchScalar && patchScalar.Style == ScalarStyle.Literal)
                {
                    var patchContent = patchScalar.Value ?? "";
                    var replacer = new YamlJson6902PatchImageReplacer(patchContent, defaultRegistry, log);
                    var result = replacer.UpdateImages(imagesToUpdate);

                    if (result.UpdatedImageReferences.Count > 0)
                    {
                        patchScalar.Value = result.UpdatedContents;
                        allUpdatedImages.UnionWith(result.UpdatedImageReferences);
                    }
                }
            }

            if (!allUpdatedImages.Any())
            {
                return new ImageReplacementResult(input, new HashSet<string>(), new HashSet<string>());
            }
            
            using var writer = new StringWriter();
            yamlStream.Save(writer, false);
            var modifiedContent = writer.ToString().TrimEnd();

            return new ImageReplacementResult(modifiedContent, allUpdatedImages, new HashSet<string>());
    }
}