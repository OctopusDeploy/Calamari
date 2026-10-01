#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

static class JsonAssert
{
    public static void Equal(JsonNode? expected, JsonNode? actual)
    {
        if (expected == null && actual == null) return;
        if (expected == null || actual == null)
        {
            Assert.Fail($"Expected {expected?.ToJsonString() ?? "null"}, but got {actual?.ToJsonString() ?? "null"}");
        }

        // Normalize both by serializing with consistent options
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = null
        };

        using var expectedDoc = JsonDocument.Parse(expected!.ToJsonString());
        using var actualDoc = JsonDocument.Parse(actual!.ToJsonString());

        // Serialize in a sorted, deterministic way
        var expectedJson = NormalizeJson(expectedDoc.RootElement);
        var actualJson = NormalizeJson(actualDoc.RootElement);

        if (expectedJson != actualJson)
        {
            Assert.Fail($"Expected:\n{FormatJson(expected.ToJsonString())}\n\nActual:\n{FormatJson(actual.ToJsonString())}");
        }
    }

    static string NormalizeJson(JsonElement element)
    {
        // For objects, sort properties alphabetically and recursively normalize values
        if (element.ValueKind == JsonValueKind.Object)
        {
            var sortedProps = new SortedDictionary<string, string>();
            foreach (var prop in element.EnumerateObject())
            {
                sortedProps[prop.Name] = NormalizeJson(prop.Value);
            }
            return JsonSerializer.Serialize(sortedProps);
        }

        // For arrays, recursively normalize each element
        if (element.ValueKind == JsonValueKind.Array)
        {
            var normalizedArray = new List<string>();
            foreach (var item in element.EnumerateArray())
            {
                normalizedArray.Add(NormalizeJson(item));
            }
            return JsonSerializer.Serialize(normalizedArray);
        }

        return JsonSerializer.Serialize(element);
    }

    static string FormatJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
}
