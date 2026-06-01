using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Calamari.Kubernetes.Patching.JsonPatch;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

#nullable enable

/// <summary>
/// Source: https://github.com/json-patch/json-patch-tests
/// This class runs the tests in spec_tests.json and tests.json
/// </summary>
[TestFixture]
public class JsonPatchDocumentApplierTests_SpecTests
{
    static readonly Lazy<List<JsonPatchTestDefinition>> SpecTests = new(() =>
    {
        var s = JsonSerializer.Deserialize<List<JsonPatchTestDefinition>>(File.ReadAllBytes("KubernetesFixtures/Patching/JsonPatch/spec_tests.json"));
        return s!;
    });

    static readonly Lazy<List<JsonPatchTestDefinition>> OtherTests = new(() =>
    {
        var s = JsonSerializer.Deserialize<List<JsonPatchTestDefinition>>(File.ReadAllBytes("KubernetesFixtures/Patching/JsonPatch/tests.json"));
        return s!;
    });

    [TestCaseSource(nameof(LoadSpecTestsJsonNames))]
    public void FromSpecTestsJson(string testComment)
    {
        var test = SpecTests.Value.FirstOrDefault(t => t.Comment == testComment);
        if (test == null) throw new InvalidOperationException($"test not found for \"{testComment}\"");
        RunTest(test);
    }

    [TestCaseSource(nameof(LoadOtherTestsJsonNames))]
    public void FromOtherTestsJson(string testComment)
    {
        var test = OtherTests.Value.FirstOrDefault(t => t.Comment == testComment);
        if (test == null) throw new InvalidOperationException($"test not found for \"{testComment}\"");
        RunTest(test);
    }

    static void RunTest(JsonPatchTestDefinition test)
    {
        if (test.Error != null)
        {
            var act = () =>
            {
                // for invalid JSON structures, we can fail at deserialize time rather than Apply time
                var patch = new JsonPatchDocument(test.Patch.Deserialize<List<JsonPatchOperation>>());
                return patch.Apply(test.Doc);
            };
            act.Should().Throw<Exception>(because: test.Error); // TODO how can we be sure it's failing for the right reason?
        }
        else if (test.Expected != null)
        {
            var patch = new JsonPatchDocument(test.Patch.Deserialize<List<JsonPatchOperation>>());
            var output = patch.Apply(test.Doc);
            JsonAssert.Equal(test.Expected, output);
        }
        else
        {
            throw new FormatException($"Spec test did not contain either Error or Expected for \"{test.Comment}\"");
        }
    }

    public static IEnumerable<object[]> LoadSpecTestsJsonNames() => SpecTests.Value.Where(t => t.Disabled != true).Select(o => new object[] { o.Comment });

    public static IEnumerable<object[]> LoadOtherTestsJsonNames() => OtherTests.Value.Where(t => t.Disabled != true).Select(o => new object[] { o.Comment });

    public record JsonPatchTestDefinition(
        [property: JsonPropertyName("comment")]
        string Comment,
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
