using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting.WindowsPowerShell;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PowerShell
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    public class PowerShellCoreOnWindowsFixture : PowerShellFixtureBase
    {
        protected override PowerShellEdition PowerShellEdition => PowerShellEdition.Core;

        [SetUp]
        public void SetUp()
        {
            var path = new WindowsPowerShellCoreBootstrapper(new WindowsPhysicalFileSystem()).PathToPowerShellExecutable(new CalamariVariables());
            if (!File.Exists(path))
            {
                CommandLineRunner clr = new CommandLineRunner(ConsoleLog.Instance, new CalamariVariables());
                var result = clr.Execute(new CommandLineInvocation("pwsh.exe", "--version") { OutputToLog = false });
                if (result.HasErrors)
                    Assert.Inconclusive("PowerShell Core is not installed on this machine");
            }
        }

        [Test]
        public void IncorrectPowerShellEditionShouldThrowException()
        {
            var nonExistentEdition = "PowerShellCore";
            var output = RunScript("Hello.ps1",
                new Dictionary<string, string>() {{PowerShellVariables.Edition, nonExistentEdition}});
            
            output.result.AssertFailure();
            output.result.AssertErrorOutput("Attempted to use 'PowerShellCore' edition of PowerShell, but this edition could not be found. Possible editions: Core, Desktop");
        }
    }
}