using System;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Integration.Scripting;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Calamari.Tests.Fixtures
{
    public class RequiresNonFreeBSDPlatformAttribute : NUnitAttribute, IApplyToTest
    {
        public void ApplyToTest(Test test)
        {
            if (ScriptingEnvironment.IsRunningOnMono() && (Environment.GetEnvironmentVariable("TEAMCITY_BUILDCONF_NAME")?.Contains("FreeBSD") ?? false))
            {
                test.RunState = RunState.Skipped;
                test.Properties.Set(PropertyNames.SkipReason, "This test does not run on FreeBSD");
            }
        }
    }
}