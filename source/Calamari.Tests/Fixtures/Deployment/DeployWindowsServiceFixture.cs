using System.IO;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Util;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
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
            TestUserPrincipal userPrincipal = null;
            try
            {
                userPrincipal = new TestUserPrincipal("calamari-svc-test")
                    .EnsureIsMemberOfGroup("Administrators")
                    .GrantLogonAsAServiceRight();
                Variables[SpecialVariables.Action.WindowsService.CustomAccountName] = userPrincipal.NTAccountName;
                Variables[SpecialVariables.Action.WindowsService.CustomAccountPassword] = userPrincipal.Password;

                RunDeployment();
            }
            finally
            {
                userPrincipal?.Delete();
            }

        }
    }
}