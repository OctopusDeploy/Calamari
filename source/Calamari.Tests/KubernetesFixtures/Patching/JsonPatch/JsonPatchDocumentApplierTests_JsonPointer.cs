using System;
using System.Text.Json.Nodes;
using Calamari.Kubernetes.Patching.JsonPatch;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

[TestFixture]
public class JsonPatchDocumentApplierTests_JsonPointer
{
    [Test]
    public void HandlesEscapedCharacters()
    {
        var source = JsonNode.Parse("""{"foo/bar": "value", "baz~qux": "value2"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Replace,
                Path = new JsonPointer("/foo~1bar"),
                Value = "new-value"
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"foo/bar": "new-value", "baz~qux": "value2"}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void HandlesTildeEscape()
    {
        var source = JsonNode.Parse("""{"foo~bar": "value"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Replace,
                Path = new JsonPointer("/foo~0bar"),
                Value = "new-value"
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"foo~bar": "new-value"}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void HandlesEmptyStringProperty()
    {
        var source = JsonNode.Parse("""{"": "empty-key-value"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Replace,
                Path = new JsonPointer("/"),
                Value = "new-value"
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"": "new-value"}""");
        JsonAssert.Equal(expected, result);
    }
}
