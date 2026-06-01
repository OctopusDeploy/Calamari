using System;
using System.Text.Json.Nodes;
using Calamari.Kubernetes.Patching.JsonPatch;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

[TestFixture]
public class JsonPatchDocumentApplierTests_Test
{
    [Test]
    public void SucceedsWhenValuesMatch()
    {
        var source = JsonNode.Parse("""{"foo": "bar"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Test,
                Path = new JsonPointer("/foo"),
                Value = "bar"
            }
        ]);

        var result = patch.Apply(source);

        JsonAssert.Equal(source, result);
    }

    [Test]
    public void SucceedsForComplexObjects()
    {
        var source = JsonNode.Parse("""{"obj": {"a": 1, "b": 2}}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Test,
                Path = new JsonPointer("/obj"),
                Value = JsonNode.Parse("""{"a": 1, "b": 2}""")
            }
        ]);

        var result = patch.Apply(source);

        JsonAssert.Equal(source, result);
    }

    [Test]
    public void ThrowsWhenValuesDontMatch()
    {
        var source = JsonNode.Parse("""{"foo": "bar"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Test,
                Path = new JsonPointer("/foo"),
                Value = "wrong"
            }
        ]);

        var ex = Assert.Throws<JsonPatchApplicationException>(() => patch.Apply(source));
        ex.Message.Should().Contain("Test operation failed");
    }

    [Test]
    public void ThrowsWhenPathNotFound()
    {
        var source = JsonNode.Parse("""{"foo": "bar"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Test,
                Path = new JsonPointer("/nonexistent"),
                Value = "value"
            }
        ]);

        Assert.Throws<JsonPatchApplicationException>(() => patch.Apply(source));
    }
}
