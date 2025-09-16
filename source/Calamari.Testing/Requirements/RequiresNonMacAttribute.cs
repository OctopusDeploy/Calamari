using System;
using Calamari.Common.Plumbing;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Calamari.Testing.Requirements
{
    public class RequiresNonMacAttribute : NUnitAttribute, IApplyToTest
    {
        readonly string reason;

        public RequiresNonMacAttribute()
        {
        }

        public RequiresNonMacAttribute(string reason, bool onlyOnTeamCity)
        {
            this.reason = reason;
        }

        public void ApplyToTest(Test test)
        {
            if (CalamariEnvironment.IsRunningOnMac)
            {
                var skipReason = "This test does not run on MacOS";
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    skipReason += $" because {reason}";
                }
                test.RunState = RunState.Skipped;
                test.Properties.Set(PropertyNames.SkipReason, skipReason);
            }
        }
    }
}
