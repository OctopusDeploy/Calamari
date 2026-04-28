using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Calamari.Kubernetes.Patching;
using Calamari.Kubernetes.Patching.JsonPatch;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public static class JsonPatchUtils
{
    // The tag should always be present — ContainerImageReference objects are created by
    // DeploymentConfigFactory which ensures a tag is set. The no-tag fallback appends the
    // placeholder anyway so the replacer can still match.
    public static string MakePlaceholderRef(string imageRef)
    {
        var colonIdx = imageRef.LastIndexOf(':');
        return colonIdx >= 0
            ? imageRef[..colonIdx] + ":__CALAMARI_PLACEHOLDER__"
            : imageRef + ":__CALAMARI_PLACEHOLDER__";
    }

    public static JsonPatchDocument CreateJsonPatchFromDiff(string originalContent, string updatedContent)
    {
        var originalStream = new YamlStream();
        originalStream.Load(new StringReader(originalContent));
        var original = new JsonArray(originalStream.Documents.Select(d => d.ToJsonNode()).ToArray());

        var updatedStream = new YamlStream();
        updatedStream.Load(new StringReader(updatedContent));
        var updated = new JsonArray(updatedStream.Documents.Select(d => d.ToJsonNode()).ToArray());

        return JsonPatchGenerator.Generate(original, updated);
    }
}
