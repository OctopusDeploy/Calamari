using System;
using System.Text.Json.Nodes;
using Calamari.Kubernetes.Patching.JsonPatch;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

[TestFixture]
public class JsonPatchDocumentApplierTests_Move
{
    [Test]
    public void MovesPropertyWithinObject()
    {
        var source = JsonNode.Parse("""{"foo": "bar", "baz": "qux"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Move,
                From = new JsonPointer("/foo"),
                Path = new JsonPointer("/renamed")
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"baz": "qux", "renamed": "bar"}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void MovesArrayElement()
    {
        var source = JsonNode.Parse("""{"arr": [1, 2, 3, 4]}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Move,
                From = new JsonPointer("/arr/1"),
                Path = new JsonPointer("/arr/3")
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"arr": [1, 3, 4, 2]}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void ThrowsWhenFromNotSpecified()
    {
        var source = JsonNode.Parse("""{"foo": "bar"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Move,
                Path = new JsonPointer("/baz")
            }
        ]);

        var ex = Assert.Throws<JsonPatchApplicationException>(() => patch.Apply(source));
        ex.Message.Should().Contain("from");
    }

    [Test]
    public void ThrowsWhenMovingToOwnChild()
    {
        var source = JsonNode.Parse("""{"foo": {"bar": "baz"}}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Move,
                From = new JsonPointer("/foo"),
                Path = new JsonPointer("/foo/bar")
            }
        ]);

        var ex = Assert.Throws<JsonPatchApplicationException>(() => patch.Apply(source));
        ex.Message.Should().Contain("own child");
    }
}
