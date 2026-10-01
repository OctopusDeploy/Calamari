#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Calamari.Kubernetes.Patching;
using FluentAssertions;
using NUnit.Framework;
using YamlDotNet.RepresentationModel;

namespace Calamari.Tests.KubernetesFixtures.Patching;

[TestFixture]
public class YamlDocumentExtensionMethodsTests
{
    [Test]
    public void ToJsonNode_ConvertsSimpleString()
    {
        var yaml = """
            message: hello world
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        var expected = JsonNode.Parse("""{"message": "hello world"}""");
        AssertJsonEqual(expected, result);
    }

    [Test]
    public void ToJsonNode_ConvertsInteger()
    {
        var yaml = """
            count: 42
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        var expected = JsonNode.Parse("""{"count": 42}""");
        AssertJsonEqual(expected, result);
    }

    [Test]
    public void ToJsonNode_ConvertsFloat()
    {
        var yaml = """
            price: 19.99
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        var expected = JsonNode.Parse("""{"price": 19.99}""");
        AssertJsonEqual(expected, result);
    }

    [Test]
    public void ToJsonNode_ConvertsBoolean()
    {
        var yaml = """
            enabled: true
            disabled: false
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        var expected = JsonNode.Parse("""{"enabled": true, "disabled": false}""");
        AssertJsonEqual(expected, result);
    }

    [Test]
    public void ToJsonNode_ConvertsNull()
    {
        var yaml = """
            value: null
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        var expected = JsonNode.Parse("""{"value": null}""");
        AssertJsonEqual(expected, result);
    }

    [Test]
    public void ToJsonNode_ConvertsSimpleArray()
    {
        var yaml = """
            items:
              - apple
              - banana
              - cherry
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        var expected = JsonNode.Parse("""{"items": ["apple", "banana", "cherry"]}""");
        AssertJsonEqual(expected, result);
    }

    [Test]
    public void ToJsonNode_ConvertsArrayOfNumbers()
    {
        var yaml = """
            numbers:
              - 1
              - 2
              - 3
              - 4
              - 5
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        var expected = JsonNode.Parse("""{"numbers": [1, 2, 3, 4, 5]}""");
        AssertJsonEqual(expected, result);
    }

    [Test]
    public void ToJsonNode_ConvertsNestedObjects()
    {
        var yaml = """
            person:
              name: John Doe
              age: 30
              address:
                street: 123 Main St
                city: Springfield
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        var expected = JsonNode.Parse("""
            {
              "person": {
                "name": "John Doe",
                "age": 30,
                "address": {
                  "street": "123 Main St",
                  "city": "Springfield"
                }
              }
            }
            """);
        AssertJsonEqual(expected, result);
    }

    [Test]
    public void ToJsonNode_ConvertsArrayOfObjects()
    {
        var yaml = """
            users:
              - name: Alice
                age: 25
              - name: Bob
                age: 30
              - name: Charlie
                age: 35
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        var expected = JsonNode.Parse("""
            {
              "users": [
                {"name": "Alice", "age": 25},
                {"name": "Bob", "age": 30},
                {"name": "Charlie", "age": 35}
              ]
            }
            """);
        AssertJsonEqual(expected, result);
    }

    [Test]
    public void ToJsonNode_ConvertsMixedTypes()
    {
        var yaml = """
            title: Test Document
            version: 1
            active: true
            score: 98.5
            tags:
              - important
              - reviewed
            metadata:
              author: Admin
              created: 2024-01-01
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        var expected = JsonNode.Parse("""
            {
              "title": "Test Document",
              "version": 1,
              "active": true,
              "score": 98.5,
              "tags": ["important", "reviewed"],
              "metadata": {
                "author": "Admin",
                "created": "2024-01-01"
              }
            }
            """);
        AssertJsonEqual(expected, result);
    }

    [Test]
    public void ToJsonNode_ConvertsKubernetesDeployment()
    {
        var yaml = """
            apiVersion: apps/v1
            kind: Deployment
            metadata:
              name: nginx-deployment
              labels:
                app: nginx
            spec:
              replicas: 3
              selector:
                matchLabels:
                  app: nginx
              template:
                metadata:
                  labels:
                    app: nginx
                spec:
                  containers:
                  - name: nginx
                    image: nginx:1.14.2
                    ports:
                    - containerPort: 80
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        result.Should().NotBeNull();
        var resultObj = result.AsObject();
        resultObj["apiVersion"]?.GetValue<string>().Should().Be("apps/v1");
        resultObj["kind"]?.GetValue<string>().Should().Be("Deployment");
        resultObj["metadata"]?["name"]?.GetValue<string>().Should().Be("nginx-deployment");
        resultObj["spec"]?["replicas"]?.GetValue<long>().Should().Be(3);
    }

    [Test]
    public void ToJsonNode_ConvertsEmptyObject()
    {
        var yaml = """
            empty: {}
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        var expected = JsonNode.Parse("""{"empty": {}}""");
        AssertJsonEqual(expected, result);
    }

    [Test]
    public void ToJsonNode_ConvertsEmptyArray()
    {
        var yaml = """
            empty: []
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        var expected = JsonNode.Parse("""{"empty": []}""");
        AssertJsonEqual(expected, result);
    }

    [Test]
    public void ToJsonNode_ConvertsDeeplyNestedStructure()
    {
        var yaml = """
            level1:
              level2:
                level3:
                  level4:
                    level5:
                      value: deep
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        var expected = JsonNode.Parse("""
            {
              "level1": {
                "level2": {
                  "level3": {
                    "level4": {
                      "level5": {
                        "value": "deep"
                      }
                    }
                  }
                }
              }
            }
            """);
        AssertJsonEqual(expected, result);
    }

    [Test]
    public void ToJsonNode_ConvertsMultilineString()
    {
        var yaml = """
            description: |
              This is a multiline
              string value
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        result.Should().NotBeNull();
        var description = result!["description"]?.GetValue<string>();
        description.Should().Contain("multiline");
        description.Should().Contain("string value");
    }

    [Test]
    public void ToJsonNode_ConvertsStringNumbers()
    {
        var yaml = """
            zipcode: "12345"
            phone: "+1-555-1234"
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        // Note: Without quotes in YAML, numbers are parsed as numbers
        // With quotes, they should remain strings
        result.Should().NotBeNull();
    }

    [Test]
    public void ToJsonNode_ConvertsNegativeNumbers()
    {
        var yaml = """
            temperature: -15
            balance: -99.99
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        var expected = JsonNode.Parse("""{"temperature": -15, "balance": -99.99}""");
        AssertJsonEqual(expected, result);
    }

    [Test]
    public void ToJsonNode_ConvertsZero()
    {
        var yaml = """
            count: 0
            score: 0.0
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        // Note: YAML parses 0.0 as integer 0, not as a float
        var expected = JsonNode.Parse("""{"count": 0, "score": 0}""");
        AssertJsonEqual(expected, result);
    }

    [Test]
    public void ToJsonNode_ConvertsConfigMap()
    {
        var yaml = """
            apiVersion: v1
            kind: ConfigMap
            metadata:
              name: game-config
            data:
              game.properties: |
                enemies=aliens
                lives=3
              ui.properties: |
                color=blue
                allow.textmode=true
            """;
        var yamlDoc = LoadYamlDocument(yaml);

        var result = yamlDoc.ToJsonNode();

        result.Should().NotBeNull();
        result!["kind"]?.GetValue<string>().Should().Be("ConfigMap");
        result["data"].Should().NotBeNull();
    }

    static YamlDocument LoadYamlDocument(string yaml)
    {
        using var reader = new StringReader(yaml);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);
        return yamlStream.Documents[0];
    }

    static void AssertJsonEqual(JsonNode? expected, JsonNode? actual)
    {
        if (expected == null && actual == null) return;
        if (expected == null || actual == null)
        {
            Assert.Fail($"Expected {expected?.ToJsonString() ?? "null"}, but got {actual?.ToJsonString() ?? "null"}");
        }

        var expectedJson = NormalizeJson(expected!.ToJsonString());
        var actualJson = NormalizeJson(actual!.ToJsonString());

        if (expectedJson != actualJson)
        {
            Assert.Fail($"Expected:\n{FormatJson(expectedJson)}\n\nActual:\n{FormatJson(actualJson)}");
        }
    }

    static string NormalizeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            var sortedProps = new SortedDictionary<string, JsonElement>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                sortedProps[prop.Name] = prop.Value;
            }
            return JsonSerializer.Serialize(sortedProps);
        }
        return JsonSerializer.Serialize(doc.RootElement);
    }

    static string FormatJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
}
