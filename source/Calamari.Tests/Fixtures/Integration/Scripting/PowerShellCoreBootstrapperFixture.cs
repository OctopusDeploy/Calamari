using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Scripting.WindowsPowerShell;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    public class PowerShellCoreBootstrapperFixture
    {
        [Test]
        public void PathToPowerShellExecutable_ShouldReturnLatestPath()
        {
            var result = GetPathToPowerShell(new [] {"6", "7-preview", "7", "8-preview"});
            
            result.Should().Be("C:\\Program Files\\PowerShell\\8-preview\\pwsh.exe");
        }

        [Test]
        public void PathToPowerShellExecutable_SpecifyingMajorVersionShouldReturnPath()
        {
            var result = GetPathToPowerShellWithCustomVersion(new [] {"6", "7"}, "6");
            
            result.Should().Be("C:\\Program Files\\PowerShell\\6\\pwsh.exe");
        }
        
        [Test]
        public void PathToPowerShellExecutable_SpecifyingMajorVersionAndPreReleaseVersionShouldReturnPath()
        {
            var result = GetPathToPowerShellWithCustomVersion(new [] {"6", "7", "7-preview"}, "7-preview");
            
            result.Should().Be("C:\\Program Files\\PowerShell\\7-preview\\pwsh.exe");
        }
        
        [Test]
        public void PathToPowerShellExecutable_IncorrectVersionShouldThrowException()
        {
            ShouldThrowPowerShellVersionNotFoundException(() =>
                GetPathToPowerShellWithCustomVersion(new [] {"7"}, "6"));
        }
        
        [Test]
        public void PathToPowerShellExecutable_ShouldThrowExceptionWhenPreReleaseTagMissingFromVersion()
        {
            ShouldThrowPowerShellVersionNotFoundException(() => 
                GetPathToPowerShellWithCustomVersion(new [] {"6", "7-preview"}, "7"));
        }

        [Test]
        public void PathToPowerShellExecutable_CustomVersionSpecifiedButNoVersionInstalledShouldThrowException()
        {
            ShouldThrowPowerShellVersionNotFoundException(() => 
                GetPathToPowerShellWithCustomVersion(Enumerable.Empty<string>(), "6"));
        }
        
        [Test]
        public void PathToPowerShellExecutable_ShouldReturnPwshWhenNoVersionsInstalledOrSpecified()
        {
            var result = GetPathToPowerShell(Enumerable.Empty<string>());
            result.Should().Be("pwsh.exe");
        }

        static void ShouldThrowPowerShellVersionNotFoundException(Action action)
        {
            action.Should().Throw<PowerShellVersionNotFoundException>();
        }

        static string GetPathToPowerShellWithCustomVersion(IEnumerable<string> versionInstalled, string customVersion)
        {
            var fileSystem = CreateFileSystem(versionInstalled);

            var variables = new CalamariVariables
            {
                {PowerShellVariables.CustomPowerShellVersion, customVersion}
            };

            return CreateBootstrapper(fileSystem).PathToPowerShellExecutable(variables);
        }

        static string GetPathToPowerShell(IEnumerable<string> versionInstalled)
        {
            var fileSystem = CreateFileSystem(versionInstalled);
            return CreateBootstrapper(fileSystem).PathToPowerShellExecutable(new CalamariVariables());
        }

        static ICalamariFileSystem CreateFileSystem(IEnumerable<string> versions)
        {
            var fileSystem = Substitute.For<ICalamariFileSystem>();

            const string parentDirectoryPath = "C:\\Program Files\\PowerShell";

            var versionsAndPaths = versions.Select(v => (version: v, path: $"{parentDirectoryPath}\\{v}")).ToArray();
            
            var powerShellPaths = versionsAndPaths.Select(vap => vap.path).ToArray();
               
            fileSystem.EnumerateDirectories(parentDirectoryPath).Returns(powerShellPaths);
            
            fileSystem.DirectoryExists(parentDirectoryPath).Returns(true);

            foreach(var (version, path) in versionsAndPaths)
                fileSystem.GetDirectoryName(path).Returns(version);

            foreach (var t in powerShellPaths)
                fileSystem.FileExists($"{t}\\pwsh.exe").Returns(true);

            return fileSystem;
        }

        static WindowsPowerShellCoreBootstrapper CreateBootstrapper(ICalamariFileSystem mockFileSystem)
        { 
            return new WindowsPowerShellCoreBootstrapper(mockFileSystem);
        }
    }
}