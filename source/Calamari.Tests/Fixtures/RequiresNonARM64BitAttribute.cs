using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using System.Runtime.InteropServices;
    
namespace Calamari.Tests.Fixtures
{
    public class RequiresNonARM64BitAttribute : NUnitAttribute, IApplyToTest
    {
        public RequiresNonARM64BitAttribute(string attributeReason)
        {
        }
        
        public void ApplyToTest(Test test)
        {
            try
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    test.RunState = RunState.Skipped;
                    test.Properties.Set(PropertyNames.SkipReason, "This test does not run on ARMx64");
                }
            }
            catch(Exception e)
            {
                if (CalamariEnvironment.IsRunningOnMono)
                {
                    Assert.Ignore("Ignoring test as Mono 4.x has problems with System.Runtime.InteropServices.RuntimeInformation");
                    return;
                }

                Assert.Fail(e.StackTrace);
            }
            
        }
    }
}