using Calamari.Common.Plumbing.Extensions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Calamari.Testing.Requirements
{
    public class RequiresDotNetFrameworkAttribute: NUnitAttribute, IApplyToTest
    {
        public void ApplyToTest(Test test)
        {
            if (!ScriptingEnvironment.IsNetFramework())
            {
                test.RunState = RunState.Skipped;
                test.Properties.Set(PropertyNames.SkipReason, "Requires dotnet Framework");
            }
        }
    }
}