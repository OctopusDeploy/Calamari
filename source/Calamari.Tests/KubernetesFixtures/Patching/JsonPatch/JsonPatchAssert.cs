#nullable enable

using System;
using Calamari.Kubernetes.Patching.JsonPatch;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

static class JsonPatchAssert
{
    public static void Equal(JsonPatchDocument expected, JsonPatchDocument actual)
    {
        if (expected.Operations.Count != actual.Operations.Count)
        {
            Assert.Fail($"Expected {expected.Operations.Count} operations, but got {actual.Operations.Count}");
        }

        for (var i = 0; i < expected.Operations.Count; i++)
        {
            Equal(expected.Operations[i], actual.Operations[i], $"Operation {i}");
        }
    }

    public static void Equal(JsonPatchOperation expected, JsonPatchOperation actual, string? context = null)
    {
        var prefix = context != null ? $"{context}: " : "";

        if (expected.Op != actual.Op)
        {
            Assert.Fail($"{prefix}Expected operation type '{expected.Op}', but got '{actual.Op}'");
        }

        if (expected.Path != actual.Path)
        {
            Assert.Fail($"{prefix}Expected path '{expected.Path}', but got '{actual.Path}'");
        }

        if (expected.From != actual.From)
        {
            Assert.Fail($"{prefix}Expected from '{expected.From}', but got '{actual.From}'");
        }

        // Compare values using JsonAssert for semantic equality
        if (expected.HasValueProperty != actual.HasValueProperty)
        {
            Assert.Fail($"{prefix}Expected HasValueProperty={expected.HasValueProperty}, but got HasValueProperty={actual.HasValueProperty}");
        }

        if (expected.HasValueProperty)
        {
            JsonAssert.Equal(expected.Value, actual.Value);
        }
    }

    public static void OperationsEqual(JsonPatchDocument patch, params JsonPatchOperation[] expectedOperations)
    {
        Equal(new JsonPatchDocument(expectedOperations), patch);
    }
}
