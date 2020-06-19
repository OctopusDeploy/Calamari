using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using System.Runtime.InteropServices;
    
namespace Calamari.Tests.Fixtures
{
    public class RequiresNonARM64BitAttribute : NUnitAttribute, IApplyToTest
    {
        public void ApplyToTest(Test test)
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                test.RunState = RunState.Skipped;
                test.Properties.Set(PropertyNames.SkipReason, "This test does not run on ARMx64");
            }
        }
    }
}