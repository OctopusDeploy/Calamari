using System;
using System.Text.Json.Nodes;
using Calamari.Kubernetes.Patching.JsonPatch;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

[TestFixture]
public class JsonPatchDocumentApplierTests_Replace
{
    [Test]
    public void ReplacesPropertyValue()
    {
        var source = JsonNode.Parse("""{"foo": "bar", "baz": "qux"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Replace,
                Path = new JsonPointer("/foo"),
                Value = "new-value"
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"foo": "new-value", "baz": "qux"}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void ReplacesArrayElement()
    {
        var source = JsonNode.Parse("""{"arr": [1, 2, 3]}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Replace,
                Path = new JsonPointer("/arr/1"),
                Value = 99
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"arr": [1, 99, 3]}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void ReplacesRoot()
    {
        var source = JsonNode.Parse("""{"foo": "bar"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Replace,
                Path = new JsonPointer(""),
                Value = JsonNode.Parse("""{"baz": "qux"}""")
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"baz": "qux"}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void ThrowsWhenPathNotFound()
    {
        var source = JsonNode.Parse("""{"foo": "bar"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Replace,
                Path = new JsonPointer("/nonexistent"),
                Value = "value"
            }
        ]);

        var ex = Assert.Throws<JsonPatchApplicationException>(() => patch.Apply(source));
        ex.Message.Should().Contain("not found");
    }
}
