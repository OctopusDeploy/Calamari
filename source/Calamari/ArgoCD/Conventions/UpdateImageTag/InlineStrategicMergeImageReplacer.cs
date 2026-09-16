using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Models;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public class InlineStrategicMergeImageReplacer : IContainerImageReplacer
{
    readonly string input;
    readonly string defaultRegistry;
    readonly ILog log;

    public InlineStrategicMergeImageReplacer(string input, string defaultRegistry, ILog log)
    {
        this.log = log;
        this.defaultRegistry = defaultRegistry;
        this.input = input;
    }

    public ImageReplacementResult UpdateImages(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        var yamlStream = YamlStreamLoader.TryLoad(input, log, "inline strategic merge patches");
        if (yamlStream?.Documents.Count != 1 || !(yamlStream.Documents[0].RootNode is YamlMappingNode rootNode))
        {
            return new ImageReplacementResult(input, new HashSet<string>(), new HashSet<string>());
        }

        if (!rootNode.Children.TryGetValue(new YamlScalarNode("patchesStrategicMerge"), out var patchesNode) || !(patchesNode is YamlSequenceNode patchSequence))
        {
            return new ImageReplacementResult(input, new HashSet<string>(), new HashSet<string>());
        }

        var allUpdatedImages = new HashSet<string>();
        var edits = new List<YamlScalarEdit>();
        foreach (var patchNode in patchSequence.Children)
        {
            // A literal block is inline patch content; a plain entry is a path to a patch file, which
            // is not ours to rewrite.
            if (patchNode is YamlScalarNode patchScalar && patchScalar.Style == ScalarStyle.Literal)
            {
                var patchContent = patchScalar.Value ?? "";
                var replacer = new ContainerImageReplacer(patchContent, defaultRegistry);
                var result = replacer.UpdateImages(imagesToUpdate);

                if (result.UpdatedImageReferences.Count > 0)
                {
                    if (!YamlScalarSplicer.CanReplaceValue(input, patchScalar))
                        throw new CommandException($"Cannot update images in the strategic merge patch on line {patchScalar.Start.Line}: {YamlScalarSplicer.DescribeUnsupportedValue(patchScalar)}.");

                    edits.Add(new YamlScalarEdit(patchScalar, result.UpdatedContents));
                    allUpdatedImages.UnionWith(result.UpdatedImageReferences);
                }
            }
        }

        if (!allUpdatedImages.Any())
        {
            return new ImageReplacementResult(input, new HashSet<string>(), new HashSet<string>());
        }

        var modifiedContent = YamlScalarSplicer.ReplaceValues(input, edits);

        return new ImageReplacementResult(modifiedContent, allUpdatedImages, new HashSet<string>());
    }
}