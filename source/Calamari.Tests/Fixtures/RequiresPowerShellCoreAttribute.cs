using Calamari.Common.Plumbing.Extensions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Calamari.Tests.Fixtures
{
    public class RequiresPowerShellCoreAttribute : NUnitAttribute, IApplyToTest
    {
        public void ApplyToTest(Test test)
        {
            if (ScriptingEnvironment.SafelyGetPowerShellVersion().Major < 6)
            {
                test.RunState = RunState.Skipped;
                test.Properties.Set(PropertyNames.SkipReason, "This test requires PowerShell Core (6) or newer.");
            }
        }
    }
}
