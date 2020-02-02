using System;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Tests.Fixtures.Util;
using Calamari.Tests.Helpers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
            fileSystem.Received().PurgeDirectory(customInstallationDirectory, Arg.Any<FailureOptions>(), new string[0]);
        }

        [Test]
        public void ShouldPassGlobsToPurgeWhenSet()
        {
            variables.Set(SpecialVariables.Package.CustomInstallationDirectory, customInstallationDirectory);
            variables.Set(SpecialVariables.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment, true.ToString());
            variables.Set(SpecialVariables.Package.CustomInstallationDirectoryPurgeExclusions, "firstglob\nsecondglob");


            CreateConvention().Install(deployment);

            // Assert we handed in the exclusion globs
            fileSystem.Received().PurgeDirectory(customInstallationDirectory, Arg.Any<FailureOptions>(), Arg.Is<string[]>(a => a[0] == "firstglob" && a[1] == "secondglob"));
        }

        [Test]
        public void ShouldNotPurgeCustomInstallationDirectoryWhenFlagIsNotSet()
        {
            variables.Set(SpecialVariables.Package.CustomInstallationDirectory, customInstallationDirectory);
            variables.Set(SpecialVariables.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment, false.ToString());

            CreateConvention().Install(deployment);

            // Assert directory was purged
            fileSystem.DidNotReceive().PurgeDirectory(customInstallationDirectory, Arg.Any<FailureOptions>());
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

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
        [ExpectedException(ExpectedMessage = "Access to the path /var/tmp/myCustomInstallDir was denied. Ensure that the application that uses this directory is not running.")]
        public void ShouldNotIncludeIISRelatedInfoInErrorMessageWhenAccessDeniedException()
        {
            variables.Set(SpecialVariables.Package.CustomInstallationDirectory, customInstallationDirectory);
            fileSystem.CopyDirectory(Arg.Any<string>(), Arg.Any<string>())
                .ThrowsForAnyArgs(
                    new UnauthorizedAccessException($"Access to the path {customInstallationDirectory} was denied."));
            CreateConvention().Install(deployment);
        }


        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        [ExpectedException(ExpectedMessage = "Access to the path C:\\myCustomInstallDir was denied. Ensure that the application that uses this directory is not running. If this is an IIS website, stop the application pool or use an app_offline.htm file (see https://g.octopushq.com/TakingWebsiteOffline).")]
        public void ShouldIncludeIISRelatedInfoInErrorMessageWhenAccessDeniedException()
        {
            variables.Set(SpecialVariables.Package.CustomInstallationDirectory, customInstallationDirectory);
            fileSystem.CopyDirectory(Arg.Any<string>(), Arg.Any<string>())
                .ThrowsForAnyArgs(
                    new UnauthorizedAccessException($"Access to the path {customInstallationDirectory} was denied."));
            CreateConvention().Install(deployment);
        }

        private CopyPackageToCustomInstallationDirectoryConvention CreateConvention()
        {
           return new CopyPackageToCustomInstallationDirectoryConvention(fileSystem); 
        }
    }
}