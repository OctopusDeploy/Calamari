using System;
using Calamari.Common.Plumbing.Extensions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Calamari.Testing.Requirements
{
    public class RequiresNonFreeBSDPlatformAttribute : NUnitAttribute, IApplyToTest
    {
        readonly string? reason;

        public RequiresNonFreeBSDPlatformAttribute()
        {
        }

        public RequiresNonFreeBSDPlatformAttribute(string reason)
        {
            this.reason = reason;
        }
        
        public void ApplyToTest(Test test)
        {
            if (ScriptingEnvironment.IsRunningOnMono() && (Environment.GetEnvironmentVariable("TEAMCITY_BUILDCONF_NAME")?.Contains("FreeBSD") ?? false))
            {
                var skipReason = "This test does not run on FreeBSD";
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