using System;
using Calamari.Common.Plumbing.Extensions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Calamari.AzureScripting.Tests
{
    // TODO: When migrating to Calamari repository this can be removed in favour of using the implementation in Calamari.Tests.Shared
    public class RequiresPowerShell5OrAboveAttribute : NUnitAttribute, IApplyToTest
    {
        public void ApplyToTest(Test test)
        {
            if (ScriptingEnvironment.SafelyGetPowerShellVersion().Major < 5)
            {
                test.RunState = RunState.Skipped;
                test.Properties.Set(PropertyNames.SkipReason, "This test requires PowerShell 5 or newer.");
            }
        }
    }
}