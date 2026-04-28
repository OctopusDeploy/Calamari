using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Calamari.Testing;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class TestPlatformsAttribute(TestPlatforms supportedPlatforms) : NUnitAttribute, ITestAction, IApplyToTest
{
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

    public ActionTargets Targets { get; set; }
    
    public void ApplyToTest(Test test)
    {
        //for each specific platform, add the category
        //if you specify Unix as your TestPlatforms, you will get Linux and MacOs as the specific platforms
        foreach (var testPlatform in TestPlatformsHelpers.GetSpecificPlatforms(supportedPlatforms)) 
        {
            test.Properties.Add(PropertyNames.Category, testPlatform.ToString());
        }
    }
}