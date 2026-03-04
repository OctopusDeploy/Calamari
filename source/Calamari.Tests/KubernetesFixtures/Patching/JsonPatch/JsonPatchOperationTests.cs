using System;
using System.Text.Json;
using Calamari.Kubernetes.Patching.JsonPatch;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

[TestFixture]
public class JsonPatchOperationTests
{
    [Test]
    public void DeserializesAddOperation()
    {
        var json = """
            {
                "op": "add",
                "path": "/foo",
                "value": "bar"
            }
            """;

        var operation = JsonSerializer.Deserialize<JsonPatchOperation>(json);

        operation.Should().NotBeNull();
        operation!.Op.Should().Be(OperationType.Add);
        operation.Path.ToString().Should().Be("/foo");
        operation.Value.Should().NotBeNull();
    }

    [Test]
    public void DeserializesRemoveOperation()
    {
        var json = """
            {
                "op": "remove",
                "path": "/foo"
            }
            """;

        var operation = JsonSerializer.Deserialize<JsonPatchOperation>(json);

        operation.Should().NotBeNull();
        operation!.Op.Should().Be(OperationType.Remove);
        operation.Path.ToString().Should().Be("/foo");
    }

    [Test]
    public void DeserializesReplaceOperation()
    {
        var json = """
            {
                "op": "replace",
                "path": "/foo",
                "value": "new-value"
            }
            """;

        var operation = JsonSerializer.Deserialize<JsonPatchOperation>(json);

        operation.Should().NotBeNull();
        operation!.Op.Should().Be(OperationType.Replace);
        operation.Path.ToString().Should().Be("/foo");
        operation.Value.Should().NotBeNull();
    }

    [Test]
    public void DeserializesMoveOperation()
    {
        var json = """
            {
                "op": "move",
                "from": "/foo",
                "path": "/bar"
            }
            """;

        var operation = JsonSerializer.Deserialize<JsonPatchOperation>(json);

        operation.Should().NotBeNull();
        operation!.Op.Should().Be(OperationType.Move);
        operation.From.Should().NotBeNull();
        operation.From!.Value.ToString().Should().Be("/foo");
        operation.Path.ToString().Should().Be("/bar");
    }

    [Test]
    public void DeserializesCopyOperation()
    {
        var json = """
            {
                "op": "copy",
                "from": "/foo",
                "path": "/bar"
            }
            """;

        var operation = JsonSerializer.Deserialize<JsonPatchOperation>(json);

        operation.Should().NotBeNull();
        operation!.Op.Should().Be(OperationType.Copy);
        operation.From.Should().NotBeNull();
        operation.From!.Value.ToString().Should().Be("/foo");
        operation.Path.ToString().Should().Be("/bar");
    }

    [Test]
    public void DeserializesTestOperation()
    {
        var json = """
            {
                "op": "test",
                "path": "/foo",
                "value": "expected"
            }
            """;

        var operation = JsonSerializer.Deserialize<JsonPatchOperation>(json);

        operation.Should().NotBeNull();
        operation!.Op.Should().Be(OperationType.Test);
        operation.Path.ToString().Should().Be("/foo");
        operation.Value.Should().NotBeNull();
    }

    [Test]
    public void DeserializesOperationWithComplexValue()
    {
        var json = """
            {
                "op": "add",
                "path": "/obj",
                "value": {
                    "nested": {
                        "prop": "value"
                    }
                }
            }
            """;

        var operation = JsonSerializer.Deserialize<JsonPatchOperation>(json);

        operation.Should().NotBeNull();
        operation!.Op.Should().Be(OperationType.Add);
        operation.Value.Should().NotBeNull();
    }

    [Test]
    public void DeserializesOperationWithArrayValue()
    {
        var json = """
            {
                "op": "add",
                "path": "/arr",
                "value": [1, 2, 3]
            }
            """;

        var operation = JsonSerializer.Deserialize<JsonPatchOperation>(json);

        operation.Should().NotBeNull();
        operation!.Op.Should().Be(OperationType.Add);
        operation.Value.Should().NotBeNull();
    }
}
