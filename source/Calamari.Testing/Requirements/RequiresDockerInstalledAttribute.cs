using System;
using Calamari.Common.Features.Processes;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Testing.Requirements
{
    public class RequiresDockerInstalledAttribute : TestAttribute, ITestAction
    {
        readonly Lazy<bool> isDockerInstalled;
        public RequiresDockerInstalledAttribute()
        {
            isDockerInstalled = new Lazy<bool>(() =>
            {
                try
                {
                    var result =
                        SilentProcessRunner.ExecuteCommand("docker", "ps -q", ".", (stdOut) => { }, (stdErr) => { });
                    return result.ExitCode == 0;
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        public void BeforeTest(ITest testDetails)
        {
            if (!isDockerInstalled.Value)
            {
                Assert.Ignore(
                    "It appears as though docker is not installed on this machine. This test will be skipped");
            }
        }

        public void AfterTest(ITest test)
        {
            
        }

        public ActionTargets Targets { get; }
    }
}