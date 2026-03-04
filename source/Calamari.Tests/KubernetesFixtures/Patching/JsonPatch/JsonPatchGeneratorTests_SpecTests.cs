#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Calamari.Kubernetes.Patching.JsonPatch;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

/// <summary>
/// Source: https://github.com/json-patch/json-patch-tests
/// This class runs the spec tests in reverse - generates patches from doc -> expected
/// and verifies they work correctly when applied.
/// </summary>
[TestFixture]
public class JsonPatchGeneratorTests_SpecTests
{
    static readonly Lazy<List<JsonPatchTestDefinition>> SpecTests = new(() =>
    {
        var s = JsonSerializer.Deserialize<List<JsonPatchTestDefinition>>(File.ReadAllBytes("KubernetesFixtures/Patching/JsonPatch/spec_tests.json"));
        return s ?? [];
    });

    static readonly Lazy<List<JsonPatchTestDefinition>> OtherTests = new(() =>
    {
        var s = JsonSerializer.Deserialize<List<JsonPatchTestDefinition>>(File.ReadAllBytes("KubernetesFixtures/Patching/JsonPatch/tests.json"));
        return s ?? [];
    });

    [TestCaseSource(nameof(LoadSpecTestsForGeneration))]
    public void GenerateFromSpecTests(int testIndex, string testLabel)
    {
        var test = SpecTests.Value[testIndex];
        RunGenerationTest(test);
    }

    [TestCaseSource(nameof(LoadOtherTestsForGeneration))]
    public void GenerateFromOtherTests(int testIndex, string testLabel)
    {
        var test = OtherTests.Value[testIndex];
        RunGenerationTest(test);
    }

    static void RunGenerationTest(JsonPatchTestDefinition test)
    {
        // Generate patch from doc -> expected
        var generated = JsonPatchGenerator.Generate(test.Doc, test.Expected);

        // Verify by applying the generated patch
        var result = generated.Apply(test.Doc);
        JsonAssert.Equal(test.Expected, result);
    }

    public static IEnumerable<object[]> LoadSpecTestsForGeneration() =>
        SpecTests.Value
            .Select((t, index) => (t, index))
            .Where(x => x.t.Disabled != true && x.t.Error == null && x.t.Expected != null)
            .Select(x => new object[] { x.index, x.t.Comment ?? $"No comment" });

    public static IEnumerable<object[]> LoadOtherTestsForGeneration() =>
        OtherTests.Value
            .Select((t, index) => (t, index))
            .Where(x => x.t.Disabled != true && x.t.Error == null && x.t.Expected != null)
            .Select(x => new object[] { x.index, x.t.Comment ?? $"No comment" });

    public record JsonPatchTestDefinition(
        [property: JsonPropertyName("comment")]
        string? Comment,
        [property: JsonPropertyName("doc")]
        JsonNode Doc,
        [property: JsonPropertyName("patch")]
        JsonNode Patch,
        [property: JsonPropertyName("error")]
        string? Error,
        [property: JsonPropertyName("expected")]
        JsonNode? Expected,
        [property: JsonPropertyName("disabled")]
        bool? Disabled);
}
