
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Calamari.Kubernetes.Patching;
using Calamari.Kubernetes.Patching.JsonPatch;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD.Git;

public static class UpdaterHelpers
{
    public static JsonPatchDocument CreateJsonPatch(string originalContent, string updatedContent)
    {
        var originalStream = new YamlStream();
        originalStream.Load(new StringReader(originalContent));
        var original = new JsonArray(originalStream.Documents.Select(d => d.ToJsonNode()).ToArray());

        var updatedStream = new YamlStream();
        updatedStream.Load(new StringReader(updatedContent));
        var updated = new JsonArray(updatedStream.Documents.Select(d => d.ToJsonNode()).ToArray());

        return JsonPatchGenerator.Generate(original, updated);
    }

    public static string Serialize(JsonPatchDocument patchDocument)
    {
        return JsonSerializer.Serialize(patchDocument);
    }
}