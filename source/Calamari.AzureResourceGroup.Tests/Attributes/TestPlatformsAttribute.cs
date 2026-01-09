using System;
using System.Collections.Generic;
using Xunit.v3;

namespace Calamari.AzureResourceGroup.Tests.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class TestPlatformsAttribute(string platform) : Attribute, ITraitAttribute
{
    public string Platform { get; } = platform;

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
        => [new("Category", Platform)];
}