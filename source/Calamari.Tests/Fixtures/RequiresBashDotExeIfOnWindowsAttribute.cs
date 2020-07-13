using System;
using System.IO;
using Calamari.Common.Plumbing;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Calamari.Tests.Fixtures
{
    public class RequiresBashDotExeIfOnWindowsAttribute : NUnitAttribute, IApplyToTest
    {
        public void ApplyToTest(Test test)
        {
            if (CalamariEnvironment.IsRunningOnWindows && !BashExeExists)
            {
                test.RunState = RunState.Skipped;
                test.Properties.Set(PropertyNames.SkipReason, "This test needs bash.exe (which comes with WSL) on windows");
            }
        }

        bool BashExeExists
        {
            get
            {
                var bashPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "bash.exe");
                return File.Exists(bashPath);
            }
        }
    }
}
