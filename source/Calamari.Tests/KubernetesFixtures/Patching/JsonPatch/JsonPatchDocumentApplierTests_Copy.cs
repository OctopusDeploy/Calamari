using System;
using System.Text.Json.Nodes;
using Calamari.Kubernetes.Patching.JsonPatch;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

[TestFixture]
public class JsonPatchDocumentApplierTests_Copy
{
    [Test]
    public void CopiesPropertyToNewLocation()
    {
        var source = JsonNode.Parse("""{"foo": "bar"}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Copy,
                From = new JsonPointer("/foo"),
                Path = new JsonPointer("/baz")
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"foo": "bar", "baz": "bar"}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void CopiesArrayElement()
    {
        var source = JsonNode.Parse("""{"arr": [1, 2, 3]}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Copy,
                From = new JsonPointer("/arr/0"),
                Path = new JsonPointer("/arr/-")
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"arr": [1, 2, 3, 1]}""");
        JsonAssert.Equal(expected, result);
    }

    [Test]
    public void CopiesComplexObject()
    {
        var source = JsonNode.Parse("""{"obj": {"a": 1, "b": 2}}""")!;
        var patch = new JsonPatchDocument(
        [
            new JsonPatchOperation
            {
                Op = OperationType.Copy,
                From = new JsonPointer("/obj"),
                Path = new JsonPointer("/copy")
            }
        ]);

        var result = patch.Apply(source);

        var expected = JsonNode.Parse("""{"obj": {"a": 1, "b": 2}, "copy": {"a": 1, "b": 2}}""");
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
                Op = OperationType.Copy,
                Path = new JsonPointer("/baz")
            }
        ]);

        var ex = Assert.Throws<JsonPatchApplicationException>(() => patch.Apply(source));
        ex.Message.Should().Contain("from");
    }
}
