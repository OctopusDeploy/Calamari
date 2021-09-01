using System;
using Calamari.Common.Plumbing;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Calamari.Tests.Shared.Requirements
{
    public class RequiresNonMacAttribute : NUnitAttribute, IApplyToTest
    {
        public void ApplyToTest(Test test)
        {
            if (CalamariEnvironment.IsRunningOnMac)
            {
                test.RunState = RunState.Skipped;
                test.Properties.Set(PropertyNames.SkipReason, "This test does not run on MacOS");
            }
        }
    }
}