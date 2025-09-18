using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Calamari.Testing.Requirements
{
    public class RequiresDotNetCoreAttribute : NUnitAttribute, IApplyToTest
    {
        static bool IsNetCore()
        {
            return true;
        }

        public void ApplyToTest(Test test)
        {
            if (!IsNetCore())
            {
                test.RunState = RunState.Skipped;
                test.Properties.Set(PropertyNames.SkipReason, "Requires dotnet core");
            }
        }
    }
}