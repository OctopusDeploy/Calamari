using System;
using System.Text.Json;
using Calamari.Kubernetes.Patching.JsonPatch;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

[TestFixture]
public class JsonPatchDocumentTests
{
    [Test]
    public void DeserializesEmptyPatchDocument()
    {
        var json = "[]";

        var document = JsonSerializer.Deserialize<JsonPatchDocument>(json);

        document.Should().NotBeNull();
        document!.Operations.Should().BeEmpty();
    }

    [Test]
    public void DeserializesSingleOperation()
    {
        var json = """
            [
                {
                    "op": "add",
                    "path": "/foo",
                    "value": "bar"
                }
            ]
            """;

        var document = JsonSerializer.Deserialize<JsonPatchDocument>(json);

        document.Should().NotBeNull();
        document!.Operations.Should().HaveCount(1);

        var op = document.Operations[0];
        op.Op.Should().Be(OperationType.Add);
        op.Path.Should().Be(new JsonPointer("/foo"));
        JsonSerializer.Serialize(op.Value).Should().Be("\"bar\"");
    }

    [Test]
    public void DeserializesMultipleOperations()
    {
        var json = """
            [
                {
                    "op": "add",
                    "path": "/foo",
                    "value": "bar"
                },
                {
                    "op": "remove",
                    "path": "/baz"
                },
                {
                    "op": "replace",
                    "path": "/qux",
                    "value": "new-value"
                }
            ]
            """;

        var document = JsonSerializer.Deserialize<JsonPatchDocument>(json);

        document.Should().NotBeNull();
        document!.Operations.Should().HaveCount(3);

        document.Operations[0].Op.Should().Be(OperationType.Add);
        document.Operations[0].Path.Should().Be(new JsonPointer("/foo"));
        JsonSerializer.Serialize(document.Operations[0].Value).Should().Be("\"bar\"");

        document.Operations[1].Op.Should().Be(OperationType.Remove);
        document.Operations[1].Path.Should().Be(new JsonPointer("/baz"));
        document.Operations[1].Value.Should().BeNull();

        document.Operations[2].Op.Should().Be(OperationType.Replace);
        document.Operations[2].Path.Should().Be(new JsonPointer("/qux"));
        JsonSerializer.Serialize(document.Operations[2].Value).Should().Be("\"new-value\"");
    }

    [Test]
    public void DeserializesComplexPatchDocument()
    {
        var json = """
            [
                {
                    "op": "test",
                    "path": "/a/b/c",
                    "value": "foo"
                },
                {
                    "op": "remove",
                    "path": "/a/b/c"
                },
                {
                    "op": "add",
                    "path": "/a/b/c",
                    "value": ["foo", "bar"]
                },
                {
                    "op": "replace",
                    "path": "/a/b/c",
                    "value": 42
                },
                {
                    "op": "move",
                    "from": "/a/b/c",
                    "path": "/a/b/d"
                },
                {
                    "op": "copy",
                    "from": "/a/b/d",
                    "path": "/a/b/e"
                }
            ]
            """;

        var document = JsonSerializer.Deserialize<JsonPatchDocument>(json);

        document.Should().NotBeNull();
        document!.Operations.Should().HaveCount(6);

        document.Operations[0].Op.Should().Be(OperationType.Test);
        document.Operations[0].Path.Should().Be(new JsonPointer("/a/b/c"));
        JsonSerializer.Serialize(document.Operations[0].Value).Should().Be("\"foo\"");

        document.Operations[1].Op.Should().Be(OperationType.Remove);
        document.Operations[1].Path.Should().Be(new JsonPointer("/a/b/c"));

        document.Operations[2].Op.Should().Be(OperationType.Add);
        document.Operations[2].Path.Should().Be(new JsonPointer("/a/b/c"));
        JsonSerializer.Serialize(document.Operations[2].Value).Should().Be("[\"foo\",\"bar\"]");

        document.Operations[3].Op.Should().Be(OperationType.Replace);
        document.Operations[3].Path.Should().Be(new JsonPointer("/a/b/c"));
        JsonSerializer.Serialize(document.Operations[3].Value).Should().Be("42");

        document.Operations[4].Op.Should().Be(OperationType.Move);
        document.Operations[4].From.Should().Be(new JsonPointer("/a/b/c"));
        document.Operations[4].Path.Should().Be(new JsonPointer("/a/b/d"));

        document.Operations[5].Op.Should().Be(OperationType.Copy);
        document.Operations[5].From.Should().Be(new JsonPointer("/a/b/d"));
        document.Operations[5].Path.Should().Be(new JsonPointer("/a/b/e"));
    }

    [Test]
    public void DeserializesRFC6902Example()
    {
        // This is the example from RFC 6902 Appendix A
        var json = """
            [
                { "op": "test", "path": "/a/b/c", "value": "foo" },
                { "op": "remove", "path": "/a/b/c" },
                { "op": "add", "path": "/a/b/c", "value": ["foo", "bar"] },
                { "op": "replace", "path": "/a/b/c", "value": 42 },
                { "op": "move", "from": "/a/b/c", "path": "/a/b/d" },
                { "op": "copy", "from": "/a/b/d", "path": "/a/b/e" }
            ]
            """;

        var document = JsonSerializer.Deserialize<JsonPatchDocument>(json);

        document.Should().NotBeNull();
        document!.Operations.Should().HaveCount(6);

        document.Operations[0].Op.Should().Be(OperationType.Test);
        document.Operations[0].Path.Should().Be(new JsonPointer("/a/b/c"));
        JsonSerializer.Serialize(document.Operations[0].Value).Should().Be("\"foo\"");

        document.Operations[1].Op.Should().Be(OperationType.Remove);
        document.Operations[1].Path.Should().Be(new JsonPointer("/a/b/c"));

        document.Operations[2].Op.Should().Be(OperationType.Add);
        document.Operations[2].Path.Should().Be(new JsonPointer("/a/b/c"));
        JsonSerializer.Serialize(document.Operations[2].Value).Should().Be("[\"foo\",\"bar\"]");

        document.Operations[3].Op.Should().Be(OperationType.Replace);
        document.Operations[3].Path.Should().Be(new JsonPointer("/a/b/c"));
        JsonSerializer.Serialize(document.Operations[3].Value).Should().Be("42");

        document.Operations[4].Op.Should().Be(OperationType.Move);
        document.Operations[4].From.Should().Be(new JsonPointer("/a/b/c"));
        document.Operations[4].Path.Should().Be(new JsonPointer("/a/b/d"));

        document.Operations[5].Op.Should().Be(OperationType.Copy);
        document.Operations[5].From.Should().Be(new JsonPointer("/a/b/d"));
        document.Operations[5].Path.Should().Be(new JsonPointer("/a/b/e"));
    }
}
