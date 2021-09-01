using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using Sashimi.Tests.Shared.Extensions;

namespace Sashimi.Tests.Shared
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class LinuxTestAttribute : NUnitAttribute, IApplyToTest
    {
        readonly string message;

        public LinuxTestAttribute() : this("This test only runs on Linux")
        {
        }

        public LinuxTestAttribute(string message)
        {
            this.message = message;
        }

        public void ApplyToTest(Test test)
        {
            if (test.RunState == RunState.NotRunnable || test.RunState == RunState.Ignored)
                return;

            if (!PlatformDetection.IsRunningOnNix)
            {
                test.RunState = RunState.Skipped;
                test.Properties.Add(PropertyNames.SkipReason, message);
            }
        }
    }
}