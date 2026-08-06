using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Principal;
using Calamari.Common.Features.Scripting.WindowsPowerShell;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Util;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Microsoft.Win32;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    [SupportedOSPlatform("Windows")]
    public class DeployWindowsServiceFixture : DeployWindowsServiceAbstractFixture
    {
        protected override string ServiceName => "Acme.Service";

        [Test]
        public void ShouldDeployAndInstallASimpleService()
        {
            RunDeployment();
        }

        [Test]
        public void ShouldDeployAndInstallWhenThereAreSpacesInThePath()
        {
            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.CustomDirectory,Octopus.Features.WindowsService";
            var installDir = Path.Combine(CustomDirectory, "A Directory With A Space In It");
            Variables[PackageVariables.CustomInstallationDirectory] = installDir;

            RunDeployment();

            Assert.IsTrue(File.Exists(Path.Combine(installDir, $"{ServiceName}.exe")), "Installed in the right location");
        }

        [Test]
        public void ShouldDeployAndInstallWhenThereAreSpacesInThePathAndArguments()
        {
            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.CustomDirectory,Octopus.Features.WindowsService";
            var installDir = Path.Combine(CustomDirectory, "A Directory With A Space In It");
            Variables[PackageVariables.CustomInstallationDirectory] = installDir;
            Variables[SpecialVariables.Action.WindowsService.Arguments] = "\"Argument with Space\" ArgumentWithoutSpace";

            RunDeployment();

            Assert.IsTrue(File.Exists(Path.Combine(installDir, $"{ServiceName}.exe")), "Installed in the right location");
        }

        [Test]
        public void ShouldDeployAndInstallWithCustomUserName()
        {
            DeployWithCustomUserNameAndAssertLogOnAccount();
        }

        [Test]
        public void ShouldDeployAndInstallWithCustomUserNameUnderPowerShellCore()
        {
            AssertPowerShellCoreIsAvailable();
            Variables[PowerShellVariables.Edition] = "Core";

            DeployWithCustomUserNameAndAssertLogOnAccount();
        }

        void DeployWithCustomUserNameAndAssertLogOnAccount()
        {
            TestUserPrincipal userPrincipal = null;
            try
            {
                userPrincipal = new TestUserPrincipal("calamari-svc-test")
                    .EnsureIsMemberOfGroup("Administrators")
                    .GrantLogonAsAServiceRight();
                Variables[SpecialVariables.Action.WindowsService.CustomAccountName] = userPrincipal.NTAccountName;
                Variables[SpecialVariables.Action.WindowsService.CustomAccountPassword] = userPrincipal.Password;

                RunDeployment(() => GetServiceLogOnAccountSid().Should().Be(userPrincipal.Sid.Value));
            }
            finally
            {
                userPrincipal?.Delete();
            }
        }

        // compared as a SID because the SCM rewrites a local account into the .\name form rather than storing what was passed
        string GetServiceLogOnAccountSid()
        {
            using (var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{ServiceName}"))
            {
                var account = key?.GetValue("ObjectName") as string;
                if (account == null)
                    return null;
                if (account.StartsWith(@".\"))
                    account = $@"{Environment.MachineName}\{account.Substring(2)}";
                return new NTAccount(account).Translate(typeof(SecurityIdentifier)).Value;
            }
        }

        // RequiresPowerShellCoreAttribute can't be used here: SafelyGetPowerShellVersion probes powershell.exe first, so it always reports 5.x on Windows
        static void AssertPowerShellCoreIsAvailable()
        {
            var path = new WindowsPowerShellCoreBootstrapper(new WindowsPhysicalFileSystem()).PathToPowerShellExecutable(new CalamariVariables());
            if (!File.Exists(path))
                Assert.Inconclusive("PowerShell Core is not installed on this machine");
        }
    }
}
