using System;
using System.IO;
using Calamari.Deployment;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Assent;
using Assent.Reporters;
using Assent.Reporters.DiffPrograms;
using Calamari.Testing.Helpers;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    public class DeployWindowsServiceArgumentsFixture : DeployWindowsServiceAbstractFixture
    {
        protected override string ServiceName => "DumpArgs";

        protected override string PackageName => "DumpArgs";

        [Test]
        public void ShouldDeployAndInstallWhenThereAreArguments()
        {
            Variables[SpecialVariables.Action.WindowsService.Arguments] = "--SomeArg -Foo \"path somewhere\"";
            RunDeployment(() =>
            {
                var argsFilePath = Path.Combine(StagingDirectory, PackageName, "1.0.0", "Args.txt");
                Assert.IsTrue(File.Exists(argsFilePath));
                this.Assent(File.ReadAllText(argsFilePath), AssentConfiguration.Default);
            });
        }

        [Test]
        public void ShouldDeployAndInstallWhenThereAreSpacesInArguments()
        {
            Variables[SpecialVariables.Action.WindowsService.Arguments] = "\"Argument with Space\" ArgumentWithoutSpace";
            RunDeployment(() =>
            {
                var argsFilePath = Path.Combine(StagingDirectory, PackageName, "1.0.0", "Args.txt");
                Assert.IsTrue(File.Exists(argsFilePath));
                this.Assent(File.ReadAllText(argsFilePath), AssentConfiguration.Default);
            });
        }
    }
}