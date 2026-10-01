using System;
using System.Text.Json.Nodes;
using Calamari.Kubernetes.Patching.JsonPatch;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

[TestFixture]
public class JsonPatchDocumentApplierTests_Remove
{
    [Test]
    public void RemovesPropertyFromObject()
    {
        var source = JsonNode.Parse("""{"foo": "bar", "baz": "qux"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Remove,
                Path = new JsonPointer("/baz")
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"foo": "bar"}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void RemovesElementFromArray()
    {
        var source = JsonNode.Parse("""{"arr": [1, 2, 3]}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Remove,
                Path = new JsonPointer("/arr/1")
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"arr": [1, 3]}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void ThrowsWhenRemovingRoot()
    {
        var source = JsonNode.Parse("""{"foo": "bar"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Remove,
                Path = new JsonPointer("")
            }
        ]);

        var ex = Assert.Throws<JsonPatchApplicationException>(() => patch.Apply(source));
        ex.Message.Should().Contain("Cannot remove root");
    }

    [Test]
    public void ThrowsWhenPathNotFound()
    {
        var source = JsonNode.Parse("""{"foo": "bar"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Remove,
                Path = new JsonPointer("/nonexistent")
            }
        ]);

        var ex = Assert.Throws<JsonPatchApplicationException>(() => patch.Apply(source));
        ex.Message.Should().Contain("not found");
    }
}
