using System;
using System.Linq;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Tests.Fixtures.Util;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class CopyPackageToCustomInstallationDirectoryConventionFixture
    {
        RunningDeployment deployment;
        ICalamariFileSystem fileSystem;
        CalamariVariableDictionary variables;
        readonly string customInstallationDirectory = (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac) ? "/var/tmp/myCustomInstallDir" : "C:\\myCustomInstallDir";
        readonly string stagingDirectory = (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac) ? "/var/tmp/applications/Acme/1.0.0" : "C:\\applications\\Acme\\1.0.0";
        readonly string packageFilePath = (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac) ? "/var/tmp/packages" : "C:\\packages";

        [SetUp]
        public void SetUp()
        {
            variables = new CalamariVariableDictionary();
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
            fileSystem.Received().PurgeDirectory(customInstallationDirectory, Arg.Is((Predicate<IFileSystemInfo>)null), Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldPurgeFileInCustomInstallationDirectoryWhenFlagAndPurgePathsAreSet()
        {
            variables.Set(SpecialVariables.Package.CustomInstallationDirectory, customInstallationDirectory);
            variables.Set(SpecialVariables.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment, true.ToString());
            variables.Set(SpecialVariables.Package.CustomInstallationDirectoryPurgePaths, "A" + Environment.NewLine + "B");

            Predicate<IFileSystemInfo> exclude = null;
            string[] expectedSearchPatterns = new string[] { "A", "B" };
            string fileName = customInstallationDirectory + ((CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac) ? "/" : "\\") + "A.txt";
            string otherFileName = customInstallationDirectory + ((CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac) ? "/" : "\\") + "B.txt";

            var fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.PurgeDirectory(Arg.Is(customInstallationDirectory), Arg.Do<Predicate<IFileSystemInfo>>(x => exclude = x), Arg.Any<FailureOptions>());
            fileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Is<string[]>(x => expectedSearchPatterns.SequenceEqual(x))).Returns(new string[] { fileName });

            CreateConvention(fileSystem).Install(deployment);

            // Assert directory was purged
            fileSystem.Received().PurgeDirectory(customInstallationDirectory, Arg.Any<Predicate<IFileSystemInfo>>(), Arg.Any<FailureOptions>());

            var file = Substitute.For<IFileSystemInfo>();
            file.FullName.Returns(fileName);
            Assert.IsFalse(exclude(file));

            file.FullName.Returns(otherFileName);
            Assert.IsTrue(exclude(file));
        }

        [Test]
        public void ShouldNotPurgeCustomInstallationDirectoryWhenFlagIsNotSet()
        {
            variables.Set(SpecialVariables.Package.CustomInstallationDirectory, customInstallationDirectory);
            variables.Set(SpecialVariables.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment, false.ToString());

            CreateConvention().Install(deployment);

            // Assert directory was not purged
            fileSystem.DidNotReceive().PurgeDirectory(customInstallationDirectory, Arg.Any<Predicate<IFileSystemInfo>>(), Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldSetCustomInstallationDirectoryVariable()
        {
            variables.Set(SpecialVariables.Package.CustomInstallationDirectory, customInstallationDirectory);
            CreateConvention().Install(deployment);
            Assert.AreEqual(variables.Get(SpecialVariables.Package.CustomInstallationDirectory), customInstallationDirectory);
        }

        [Test]
        [ExpectedException(ExpectedMessage = "An error occurred when evaluating the value for the custom install directory. The following tokens were unable to be evaluated: '#{CustomInstalDirectory}'")]
        public void ShouldFailIfCustomInstallationDirectoryVariableIsNotEvaluated()
        {
            variables.Set("CustomInstallDirectory", customInstallationDirectory);
            variables.Set(SpecialVariables.Package.CustomInstallationDirectory, "#{CustomInstalDirectory}");

            CreateConvention().Install(deployment);
        }

        [Test]
        [ExpectedException(ExpectedMessage = "The custom install directory 'relative/path/to/folder' is a relative path, please specify the path as an absolute path or a UNC path.")]
        public void ShouldFailIfCustomInstallationDirectoryIsRelativePath()
        {
            const string relativeInstallDirectory = "relative/path/to/folder";
            variables.Set(SpecialVariables.Package.CustomInstallationDirectory, relativeInstallDirectory);

            CreateConvention().Install(deployment);
        }

        private CopyPackageToCustomInstallationDirectoryConvention CreateConvention()
        {
            return new CopyPackageToCustomInstallationDirectoryConvention(fileSystem);
        }

        private CopyPackageToCustomInstallationDirectoryConvention CreateConvention(ICalamariFileSystem fileSystem)
        {
            return new CopyPackageToCustomInstallationDirectoryConvention(fileSystem);
        }
    }
}