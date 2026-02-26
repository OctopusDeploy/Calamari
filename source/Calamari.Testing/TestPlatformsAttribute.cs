using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Testing;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class TestPlatformsAttribute(TestPlatforms supportedPlatforms) : TestAttribute, ITestAction
{
    readonly TestPlatforms supportedPlatforms = supportedPlatforms;

    string GetSkipReason() =>
        $"This test only runs on: {string.Join(", ", TestPlatformsHelpers.GetSpecificPlatforms(supportedPlatforms))}";

    public void BeforeTest(ITest test)
    {
        var currentPlatform = TestPlatformsHelpers.GetCurrentPlatform();

        if ((currentPlatform & supportedPlatforms) == 0)
        {
            Assert.Ignore(GetSkipReason());
        }
    }

    public void AfterTest(ITest test)
    {
    }

    ActionTargets ITestAction.Targets { get; }
}