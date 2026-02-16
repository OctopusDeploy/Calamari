#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Calamari.Kubernetes.Patching;
using NUnit.Framework;
using YamlDotNet.RepresentationModel;

namespace Calamari.Tests.KubernetesFixtures.Patching;

[TestFixture]
public class JsonNodeExtensionMethodsTests
{
    [Test]
    public void ToYamlNode_ConvertsSimpleString()
    {
        var json = JsonNode.Parse("""{"message": "hello world"}""")!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("message: \"hello world\"", result);
    }

    [Test]
    public void ToYamlNode_ConvertsInteger()
    {
        var json = JsonNode.Parse("""{"count": 42}""")!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("count: 42", result);
    }

    [Test]
    public void ToYamlNode_ConvertsFloat()
    {
        var json = JsonNode.Parse("""{"price": 19.99}""")!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("price: 19.99", result);
    }

    [Test]
    public void ToYamlNode_ConvertsBoolean()
    {
        var json = JsonNode.Parse("""{"enabled": true, "disabled": false}""")!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("""
                             enabled: true
                             disabled: false
                             """, result);
    }

    [Test]
    public void ToYamlNode_ConvertsNull()
    {
        var json = JsonNode.Parse("""{"value": null}""")!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("value: null", result);
    }

    [Test]
    public void ToYamlNode_ConvertsSimpleArray()
    {
        var json = JsonNode.Parse("""{"items": ["apple", "banana", "cherry"]}""")!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("""
                             items:
                               - "apple"
                               - "banana"
                               - "cherry"
                             """, result);
    }

    [Test]
    public void ToYamlNode_ConvertsArrayOfNumbers()
    {
        var json = JsonNode.Parse("""{"numbers": [1, 2, 3, 4, 5]}""")!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("""
                             numbers:
                               - 1
                               - 2
                               - 3
                               - 4
                               - 5
                             """, result);
    }

    [Test]
    public void ToYamlNode_ConvertsNestedObjects()
    {
        var json = JsonNode.Parse("""
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
                                  """)!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("""
                             person:
                               name: "John Doe"
                               age: 30
                               address:
                                 street: "123 Main St"
                                 city: "Springfield"
                             """, result);
    }

    [Test]
    public void ToYamlNode_ConvertsArrayOfObjects()
    {
        var json = JsonNode.Parse("""
                                  {
                                    "users": [
                                      {"name": "Alice", "age": 25},
                                      {"name": "Bob", "age": 30},
                                      {"name": "Charlie", "age": 35}
                                    ]
                                  }
                                  """)!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("""
                             users:
                               - name: "Alice"
                                 age: 25
                               - name: "Bob"
                                 age: 30
                               - name: "Charlie"
                                 age: 35
                             """, result);
    }

    [Test]
    public void ToYamlNode_ConvertsMixedTypes()
    {
        var json = JsonNode.Parse("""
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
                                  """)!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("""
                             title: "Test Document"
                             version: 1
                             active: true
                             score: 98.5
                             tags:
                               - "important"
                               - "reviewed"
                             metadata:
                               author: "Admin"
                               created: "2024-01-01"
                             """, result);
    }

    [Test]
    public void ToYamlNode_ConvertsKubernetesDeployment()
    {
        var json = JsonNode.Parse("""
                                  {
                                    "apiVersion": "apps/v1",
                                    "kind": "Deployment",
                                    "metadata": {
                                      "name": "nginx-deployment",
                                      "labels": {
                                        "app": "nginx"
                                      }
                                    },
                                    "spec": {
                                      "replicas": 3,
                                      "selector": {
                                        "matchLabels": {
                                          "app": "nginx"
                                        }
                                      },
                                      "template": {
                                        "metadata": {
                                          "labels": {
                                            "app": "nginx"
                                          }
                                        },
                                        "spec": {
                                          "containers": [
                                            {
                                              "name": "nginx",
                                              "image": "nginx:1.14.2",
                                              "ports": [
                                                {
                                                  "containerPort": 80
                                                }
                                              ]
                                            }
                                          ]
                                        }
                                      }
                                    }
                                  }
                                  """)!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("""
                             apiVersion: "apps/v1"
                             kind: "Deployment"
                             metadata:
                               name: "nginx-deployment"
                               labels:
                                 app: "nginx"
                             spec:
                               replicas: 3
                               selector:
                                 matchLabels:
                                   app: "nginx"
                               template:
                                 metadata:
                                   labels:
                                     app: "nginx"
                                 spec:
                                   containers:
                                     - name: "nginx"
                                       image: "nginx:1.14.2"
                                       ports:
                                         - containerPort: 80
                             """, result);
    }

    [Test]
    public void ToYamlNode_ConvertsEmptyObject()
    {
        var json = JsonNode.Parse("""{"empty": {}}""")!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("empty: {}", result);
    }

    [Test]
    public void ToYamlNode_ConvertsEmptyArray()
    {
        var json = JsonNode.Parse("""{"empty": []}""")!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("empty: []", result);
    }

    [Test]
    public void ToYamlNode_ConvertsDeeplyNestedStructure()
    {
        var json = JsonNode.Parse("""
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
                                  """)!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("""
                             level1:
                               level2:
                                 level3:
                                   level4:
                                     level5:
                                       value: "deep"
                             """, result);
    }

    [Test]
    public void ToYamlNode_ConvertsNegativeNumbers()
    {
        var json = JsonNode.Parse("""{"temperature": -15, "balance": -99.99}""")!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("""
                             temperature: -15
                             balance: -99.99
                             """, result);
    }

    [Test]
    public void ToYamlNode_ConvertsZero()
    {
        var json = JsonNode.Parse("""{"count": 0, "score": 0.0}""")!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("""
                             count: 0
                             score: 0
                             """, result);
    }

    [Test]
    public void ToYamlNode_ConvertsConfigMap()
    {
        var json = JsonNode.Parse("""
                                  {
                                    "apiVersion": "v1",
                                    "kind": "ConfigMap",
                                    "metadata": {
                                      "name": "game-config"
                                    },
                                    "data": {
                                      "game.properties": "enemies=aliens\nlives=3",
                                      "ui.properties": "color=blue\nallow.textmode=true"
                                    }
                                  }
                                  """)!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("""
                             apiVersion: "v1"
                             kind: "ConfigMap"
                             metadata:
                               name: "game-config"
                             data:
                               game.properties: "enemies=aliens\nlives=3"
                               ui.properties: "color=blue\nallow.textmode=true"
                             """, result);
    }

    [Test]
    public void ToYamlNode_RoundTrip_PreservesStructure()
    {
        var originalJson = JsonNode.Parse("""
                                          {
                                            "name": "test",
                                            "count": 42,
                                            "enabled": true,
                                            "items": ["a", "b", "c"],
                                            "nested": {
                                              "value": 123
                                            }
                                          }
                                          """)!;

        var yamlNode = originalJson.ToYamlNode();
        var yamlDoc = new YamlDocument(yamlNode);
        var roundTripJson = yamlDoc.ToJsonNode();

        // Compare structure
        AssertJsonNodesEqual(originalJson, roundTripJson);
    }

    [Test]
    public void ToYamlNode_HandlesSpecialCharactersInStrings()
    {
        var json = JsonNode.Parse("""{"message": "Hello: world with \"quotes\" and 'apostrophes'"}""")!;

        var result = json.ToYamlNode();

        // Round-trip through YAML to verify it preserves the string value correctly
        var yamlDoc = new YamlDocument(result);
        var roundTripJson = yamlDoc.ToJsonNode();
        AssertJsonNodesEqual(json, roundTripJson);
    }

    [Test]
    public void ToYamlNode_HandlesLargeNumbers()
    {
        var json = JsonNode.Parse("""{"bigNumber": 9223372036854775807}""")!;

        var result = json.ToYamlNode();

        AssertYamlNodesEqual("bigNumber: 9223372036854775807", result);
    }

    static string ConvertToYamlString(YamlNode yamlNode)
    {
        var yamlDoc = new YamlDocument(yamlNode);
        var yamlStream = new YamlStream(yamlDoc);

        using var writer = new StringWriter();
        yamlStream.Save(writer, assignAnchors: false);
        return writer.ToString();
    }

    static void AssertYamlNodesEqual(string expectedYaml, YamlNode actualYamlNode)
    {
        // Parse expected YAML string
        using var expectedReader = new StringReader(expectedYaml);
        var expectedStream = new YamlStream();
        expectedStream.Load(expectedReader);
        var expectedYamlDoc = expectedStream.Documents[0];

        // Convert both to JSON and compare semantically (order-independent)
        var expectedJson = expectedYamlDoc.ToJsonNode();
        var actualYamlDoc = new YamlDocument(actualYamlNode);
        var actualJson = actualYamlDoc.ToJsonNode();

        AssertJsonNodesEqual(expectedJson, actualJson);
    }

    static void AssertJsonNodesEqual(JsonNode? expected, JsonNode? actual)
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
            Assert.Fail($"Expected:\n{expectedJson}\n\nActual:\n{actualJson}");
        }
    }

    static string NormalizeJson(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var sortedProps = new SortedDictionary<string, System.Text.Json.JsonElement>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                sortedProps[prop.Name] = prop.Value;
            }

            return System.Text.Json.JsonSerializer.Serialize(sortedProps);
        }

        return System.Text.Json.JsonSerializer.Serialize(doc.RootElement);
    }
}
