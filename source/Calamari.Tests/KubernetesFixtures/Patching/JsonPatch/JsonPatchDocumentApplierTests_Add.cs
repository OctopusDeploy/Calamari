using System;
using System.Text.Json.Nodes;
using Calamari.Kubernetes.Patching.JsonPatch;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

[TestFixture]
public class JsonPatchDocumentApplierTests_Add
{
    [Test]
    public void AddsPropertyToObject()
    {
        var source = JsonNode.Parse("""{"foo": "bar"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Add,
                Path = new JsonPointer("/baz"),
                Value = "qux"
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"foo": "bar", "baz": "qux"}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void AddsElementToArray()
    {
        var source = JsonNode.Parse("""{"arr": [1, 2, 3]}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Add,
                Path = new JsonPointer("/arr/1"),
                Value = 99
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"arr": [1, 99, 2, 3]}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void AppendsToArrayWithDash()
    {
        var source = JsonNode.Parse("""{"arr": [1, 2, 3]}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Add,
                Path = new JsonPointer("/arr/-"),
                Value = 4
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"arr": [1, 2, 3, 4]}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void ReplacesRootDocument()
    {
        var source = JsonNode.Parse("""{"foo": "bar"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Add,
                Path = new JsonPointer(""),
                Value = JsonNode.Parse("""{"baz": "qux"}""")
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"baz": "qux"}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void AddsObjectValue()
    {
        var source = JsonNode.Parse("""{"foo": "bar"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Add,
                Path = new JsonPointer("/child"),
                Value = JsonNode.Parse("""{"name": "John"}""")
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"foo": "bar", "child": {"name": "John"}}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void ThrowsWhenArrayIndexOutOfBounds()
    {
        var source = JsonNode.Parse("""{"arr": [1, 2, 3]}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Add,
                Path = new JsonPointer("/arr/10"),
                Value = 99
            }
        ]);

        var ex = Assert.Throws<JsonPatchApplicationException>(() => patch.Apply(source));
        ex.InnerException.Message.Should().Contain("out of bounds");
    }
}
