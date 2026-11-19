using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Calamari.Testing;

/// <summary>
/// Restricts a test to one or more specific platforms and emits NUnit Categories (e.g. <c>Category=PlatformWindows</c>) used by CI to
/// filter which tests are collected.
/// </summary>
/// <remarks>
/// Use this attribute only when the test exercises behavior that genuinely differs between platforms.
/// This is based on https://github.com/OctopusDeploy/OctopusDeploy/blob/main/source/Octopus.Tests.Common/TestInfrastructure/TestPlatformsAttribute.cs
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class TestPlatformsAttribute(TestPlatforms supportedPlatforms) : NUnitAttribute, IApplyToTest
{
    public void ApplyToTest(Test test)
    {
        var currentPlatform = TestPlatformsHelpers.GetCurrentPlatform();

        //if this platform isn't in the supported platforms value
        if ((currentPlatform & supportedPlatforms) == 0)
        {
            test.RunState = RunState.Skipped;
            test.Properties.Add(PropertyNames.SkipReason, GetSkipReason());
        }

        //add the support platforms to the categories list
        foreach (var platform in TestPlatformsHelpers.GetSpecificPlatforms(supportedPlatforms))
        {
            test.Properties.Add(PropertyNames.Category, $"Platform{platform}");
        }
    }

    string GetSkipReason() =>
        $"This test only runs on: {string.Join(", ", TestPlatformsHelpers.GetSpecificPlatforms(supportedPlatforms))}";
}