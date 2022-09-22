using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using OSPlatform = System.Runtime.InteropServices.OSPlatform;

namespace Calamari.Testing.Requirements
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class WindowsTestAttribute : NUnitAttribute, IApplyToTest
    {
        readonly string message;

        public WindowsTestAttribute() : this("This test only runs on Windows")
        {
        }

        public WindowsTestAttribute(string message)
        {
            this.message = message;
        }
        
        static bool IsRunningOnWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public void ApplyToTest(Test test)
        {
            if (test.RunState == RunState.NotRunnable || test.RunState == RunState.Ignored)
                return;

            if (!IsRunningOnWindows)
            {
                test.RunState = RunState.Skipped;
                test.Properties.Add(PropertyNames.SkipReason, message);
            }
        }
    }
}