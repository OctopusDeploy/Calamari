using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    [Category(TestEnvironment.CompatableOS.All)]
    public class CopyPackageToCustomInstallationDirectoryConventionFixture
    {
        RunningDeployment deployment;
        ICalamariFileSystem fileSystem;
        VariableDictionary variables;
        const string customInstallationDirectory = "C:\\myCustomInstallDir";
        const string stagingDirectory = "C:\\applications\\Acme\\1.0.0";
        const string packageFilePath = "C:\\packages";

        [SetUp]
        public void SetUp()
        {
            variables = new VariableDictionary();
            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, stagingDirectory);
            fileSystem = Substitute.For<ICalamariFileSystem>();
            deployment = new RunningDeployment(packageFilePath, variables);
        }

        [Test]
        public void ShouldCopyFilesWhenCustomInstallationDirectoryIsSupplied()
        {
            variables.Set(SpecialVariables.Package.CustomInstallationDirectory, customInstallationDirectory);
            CreateConvention().Install(deployment);
            fileSystem.Received().CopyDirectory( stagingDirectory, customInstallationDirectory);
        }

        [Test]
        public void ShouldNotCopyFilesWhenCustomInstallationDirectoryNotSupplied()
        {
            CreateConvention().Install(deployment);
            fileSystem.DidNotReceive().CopyDirectory(Arg.Any<string>(), customInstallationDirectory);
        }

        [Test]
        public void ShouldPurgeCustomInstallationDirectoryWhenFlagIsSet()
        {
            variables.Set(SpecialVariables.Package.CustomInstallationDirectory, customInstallationDirectory);
            variables.Set(SpecialVariables.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment, true.ToString());

            CreateConvention().Install(deployment);

            // Assert directory was purged
            fileSystem.Received().PurgeDirectory(customInstallationDirectory, Arg.Any<DeletionOptions>());
        }

        [Test]
        public void ShouldNotPurgeCustomInstallationDirectoryWhenFlagIsNotSet()
        {
            variables.Set(SpecialVariables.Package.CustomInstallationDirectory, customInstallationDirectory);
            variables.Set(SpecialVariables.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment, false.ToString());

            CreateConvention().Install(deployment);

            // Assert directory was purged
            fileSystem.DidNotReceive().PurgeDirectory(customInstallationDirectory, Arg.Any<DeletionOptions>());
        }

        [Test]
        public void ShouldSetCustomInstallationDirectoryVariable()
        {
            variables.Set(SpecialVariables.Package.CustomInstallationDirectory, customInstallationDirectory);
            CreateConvention().Install(deployment);
            Assert.AreEqual(variables.Get(SpecialVariables.Package.CustomInstallationDirectory), customInstallationDirectory);
        }


        private CopyPackageToCustomInstallationDirectoryConvention CreateConvention()
        {
           return new CopyPackageToCustomInstallationDirectoryConvention(fileSystem); 
        }
    }
}