using System;
using System.Text.Json.Nodes;
using Calamari.Kubernetes.Patching.JsonPatch;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

[TestFixture]
public class JsonPatchDocumentApplierTests_Complex
{
    [Test]
    public void AppliesMultipleOperationsInSequence()
    {
        var source = JsonNode.Parse("""{"foo": "bar", "arr": [1, 2, 3]}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation { Op = OperationType.Add, Path = new JsonPointer("/baz"), Value = "qux" },
            new JsonPatchOperation { Op = OperationType.Remove, Path = new JsonPointer("/foo") },
            new JsonPatchOperation { Op = OperationType.Add, Path = new JsonPointer("/arr/-"), Value = 4 }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"baz": "qux", "arr": [1, 2, 3, 4]}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void WorksWithDeeplyNestedStructures()
    {
        var source = JsonNode.Parse("""{"a": {"b": {"c": {"d": "value"}}}}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Replace,
                Path = new JsonPointer("/a/b/c/d"),
                Value = "new-value"
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"a": {"b": {"c": {"d": "new-value"}}}}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void DoesNotMutateOriginalDocument()
    {
        var source = JsonNode.Parse("""{"foo": "bar"}""")!;
        var originalJson = source.ToJsonString();
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Add,
                Path = new JsonPointer("/baz"),
                Value = "qux"
            }
        ]);

        patch.Apply(source);

        source.ToJsonString().Should().Be(originalJson);
    }

    [Test]
    public void AbortsOnFirstFailure()
    {
        var source = JsonNode.Parse("""{"foo": "bar"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation { Op = OperationType.Add, Path = new JsonPointer("/baz"), Value = "qux" },
            new JsonPatchOperation { Op = OperationType.Remove, Path = new JsonPointer("/nonexistent") },
            new JsonPatchOperation { Op = OperationType.Add, Path = new JsonPointer("/wont-be-applied"), Value = "value" }
        ]);

        Assert.Throws<JsonPatchApplicationException>(() => patch.Apply(source));
    }
}
