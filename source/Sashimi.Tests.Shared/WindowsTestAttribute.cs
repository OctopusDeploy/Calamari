using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using Sashimi.Tests.Shared.Extensions;

namespace Sashimi.Tests.Shared
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

        public void ApplyToTest(Test test)
        {
            if (test.RunState == RunState.NotRunnable || test.RunState == RunState.Ignored)
                return;

            if (!PlatformDetection.IsRunningOnWindows)
            {
                test.RunState = RunState.Skipped;
                test.Properties.Add(PropertyNames.SkipReason, message);
            }
        }
    }
}