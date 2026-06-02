#nullable enable

using System;
using System.Text.Json.Nodes;
using Calamari.Kubernetes.Patching.JsonPatch;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

[TestFixture]
public class JsonPatchGeneratorTests
{
    [Test]
    public void IdenticalDocuments_GeneratesEmptyPatch()
    {
        // Arrange
        var original = JsonNode.Parse("""{"name": "Alice", "age": 30}""");
        var modified = JsonNode.Parse("""{"name": "Alice", "age": 30}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        patch.Operations.Should().BeEmpty();
    }

    [Test]
    public void AddProperty_GeneratesAddOperation()
    {
        // Arrange
        var original = JsonNode.Parse("""{"name": "Alice"}""");
        var modified = JsonNode.Parse("""{"name": "Alice", "age": 30}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Add(new JsonPointer("/age"), JsonValue.Create(30)));
    }

    [Test]
    public void RemoveProperty_GeneratesRemoveOperation()
    {
        // Arrange
        var original = JsonNode.Parse("""{"name": "Alice", "age": 30}""");
        var modified = JsonNode.Parse("""{"name": "Alice"}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Remove(new JsonPointer("/age")));
    }

    [Test]
    public void ReplaceValue_GeneratesReplaceOperation()
    {
        // Arrange
        var original = JsonNode.Parse("""{"name": "Alice", "age": 30}""");
        var modified = JsonNode.Parse("""{"name": "Alice", "age": 31}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Replace(new JsonPointer("/age"), JsonValue.Create(31)));
    }

    [Test]
    public void ReplaceRoot_GeneratesRemoveAndAddOperations()
    {
        // Arrange
        var original = JsonNode.Parse("""{"name": "Alice"}""");
        var modified = JsonNode.Parse("""{"city": "NYC"}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Remove(new JsonPointer("/name")),
            JsonPatchOperation.Add(new JsonPointer("/city"), JsonValue.Create("NYC")));
    }

    [Test]
    public void NestedObjectChanges_GeneratesReplaceAndAddOperations()
    {
        // Arrange
        var original = JsonNode.Parse("""{"person": {"name": "Alice", "age": 30}}""");
        var modified = JsonNode.Parse("""{"person": {"name": "Alice", "age": 31, "city": "NYC"}}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Replace(new JsonPointer("/person/age"), JsonValue.Create(31)),
            JsonPatchOperation.Add(new JsonPointer("/person/city"), JsonValue.Create("NYC")));
    }

    [Test]
    public void ArrayElementModification_GeneratesReplaceOperation()
    {
        // Arrange
        var original = JsonNode.Parse("""{"items": [1, 2, 3]}""");
        var modified = JsonNode.Parse("""{"items": [1, 5, 3]}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Replace(new JsonPointer("/items/1"), JsonValue.Create(5)));
    }

    [Test]
    public void ArrayGrowth_GeneratesAddOperationsWithAppendSyntax()
    {
        // Arrange
        var original = JsonNode.Parse("""{"items": [1, 2]}""");
        var modified = JsonNode.Parse("""{"items": [1, 2, 3, 4]}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Add(new JsonPointer("/items/-"), JsonValue.Create(3)),
            JsonPatchOperation.Add(new JsonPointer("/items/-"), JsonValue.Create(4)));
    }

    [Test]
    public void ArrayShrinkage_GeneratesRemoveOperationsInReverseOrder()
    {
        // Arrange
        var original = JsonNode.Parse("""{"items": [1, 2, 3, 4]}""");
        var modified = JsonNode.Parse("""{"items": [1, 2]}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Remove(new JsonPointer("/items/3")),
            JsonPatchOperation.Remove(new JsonPointer("/items/2")));
    }

    [Test]
    public void TypeChange_StringToNumber_GeneratesReplaceOperation()
    {
        // Arrange
        var original = JsonNode.Parse("""{"value": "123"}""");
        var modified = JsonNode.Parse("""{"value": 123}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Replace(new JsonPointer("/value"), JsonValue.Create(123)));
    }

    [Test]
    public void TypeChange_ObjectToArray_GeneratesReplaceOperation()
    {
        // Arrange
        var original = JsonNode.Parse("""{"data": {"a": 1}}""");
        var modified = JsonNode.Parse("""{"data": [1, 2, 3]}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Replace(new JsonPointer("/data"), JsonNode.Parse("[1, 2, 3]")));
    }

    [Test]
    public void NullValue_AsPropertyValue_GeneratesAddOperationWithNullValue()
    {
        // Arrange
        var original = JsonNode.Parse("""{"name": "Alice"}""");
        var modified = JsonNode.Parse("""{"name": "Alice", "age": null}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Add(new JsonPointer("/age"), null));
    }

    [Test]
    public void EmptyObject_ToObjectWithProperties_GeneratesAddOperations()
    {
        // Arrange
        var original = JsonNode.Parse("""{}""");
        var modified = JsonNode.Parse("""{"name": "Alice", "age": 30}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Add(new JsonPointer("/name"), JsonValue.Create("Alice")),
            JsonPatchOperation.Add(new JsonPointer("/age"), JsonValue.Create(30)));
    }

    [Test]
    public void EmptyArray_ToArrayWithElements_GeneratesAddOperationsWithAppendSyntax()
    {
        // Arrange
        var original = JsonNode.Parse("""{"items": []}""");
        var modified = JsonNode.Parse("""{"items": [1, 2, 3]}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Add(new JsonPointer("/items/-"), JsonValue.Create(1)),
            JsonPatchOperation.Add(new JsonPointer("/items/-"), JsonValue.Create(2)),
            JsonPatchOperation.Add(new JsonPointer("/items/-"), JsonValue.Create(3)));
    }

    [Test]
    public void InputDocuments_AreNotMutated()
    {
        // Arrange
        var originalJson = """{"name": "Alice", "age": 30}""";
        var modifiedJson = """{"name": "Bob", "age": 25}""";
        var original = JsonNode.Parse(originalJson);
        var modified = JsonNode.Parse(modifiedJson);

        var originalCopy = JsonNode.Parse(originalJson);
        var modifiedCopy = JsonNode.Parse(modifiedJson);

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert - verify operations generated
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Replace(new JsonPointer("/name"), JsonValue.Create("Bob")),
            JsonPatchOperation.Replace(new JsonPointer("/age"), JsonValue.Create(25)));

        // Assert - original documents should be unchanged semantically
        patch.Apply(original);
        JsonAssert.Equal(originalCopy, original);
        JsonAssert.Equal(modifiedCopy, modified);
    }

    [Test]
    public void ComplexNested_Document_GeneratesMultipleOperations()
    {
        // Arrange
        var original = JsonNode.Parse("""
                                      {
                                          "users": [
                                              {"name": "Alice", "roles": ["admin", "user"]},
                                              {"name": "Bob", "roles": ["user"]}
                                          ],
                                          "settings": {
                                              "theme": "dark",
                                              "notifications": true
                                          }
                                      }
                                      """);

        var modified = JsonNode.Parse("""
                                      {
                                          "users": [
                                              {"name": "Alice", "roles": ["admin", "user", "moderator"]},
                                              {"name": "Charlie", "roles": ["user"]}
                                          ],
                                          "settings": {
                                              "theme": "light",
                                              "notifications": true,
                                              "language": "en"
                                          }
                                      }
                                      """);

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Add(new JsonPointer("/users/0/roles/-"), JsonValue.Create("moderator")),
            JsonPatchOperation.Replace(new JsonPointer("/users/1/name"), JsonValue.Create("Charlie")),
            JsonPatchOperation.Replace(new JsonPointer("/settings/theme"), JsonValue.Create("light")),
            JsonPatchOperation.Add(new JsonPointer("/settings/language"), JsonValue.Create("en")));
    }

    [Test]
    public void PropertyNamesWithSpecialCharacters_GeneratesAddOperationsWithEscaping()
    {
        // Arrange
        var original = JsonNode.Parse("""{"normal": 1}""");
        var modified = JsonNode.Parse("""{"normal": 1, "with/slash": 2, "with~tilde": 3}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Add(new JsonPointer("/with~1slash"), JsonValue.Create(2)),
            JsonPatchOperation.Add(new JsonPointer("/with~0tilde"), JsonValue.Create(3)));
    }

    [Test]
    public void BothNull_GeneratesEmptyPatch()
    {
        // Arrange
        JsonNode? original = null;
        JsonNode? modified = null;

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        patch.Operations.Should().BeEmpty();
    }

    [Test]
    public void ReplacePropertyValueWithNull_GeneratesReplaceOperationWithNullValue()
    {
        // Arrange
        var original = JsonNode.Parse("""{"foo": "bar"}""");
        var modified = JsonNode.Parse("""{"foo": null}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        JsonPatchAssert.OperationsEqual(patch,
            JsonPatchOperation.Replace(new JsonPointer("/foo"), null));
    }

    [Test]
    public void OriginalNull_ModifiedExists_GeneratesReplaceAtRoot()
    {
        // Arrange
        JsonNode? original = null;
        var modified = JsonNode.Parse("""{"name": "Alice"}""");

        // Act
        var patch = JsonPatchGenerator.Generate(original, modified);

        // Assert
        patch.Operations.Should().HaveCount(1);
        patch.Operations[0].Op.Should().Be(OperationType.Replace);
        patch.Operations[0].Path.IsEmpty.Should().BeTrue();
    }

}
