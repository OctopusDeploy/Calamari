using System;
using System.Linq;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting.WindowsPowerShell;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.Windows)]
    public class PowerShellCoreBootstrapperFixture
    {
        [Test]
        public void ShouldReturnLatestPath()
        {
            var mockFileSystem = Substitute.For<ICalamariFileSystem>();
            SetUpMockFileSystem(mockFileSystem);
            
            var pathToPowerShell = CreatePowerShellCoreBootstrapper(mockFileSystem).PathToPowerShellExecutable(new CalamariVariableDictionary());
            pathToPowerShell.Should().Be("C:\\Program Files\\PowerShell\\8-preview\\pwsh.exe");
        }

        [Test]
        public void SpecifyingVersionShouldReturnPath()
        {
            var mockFileSystem = Substitute.For<ICalamariFileSystem>();
            SetUpMockFileSystem(mockFileSystem);

            var variables = new CalamariVariableDictionary();
            variables.Add(SpecialVariables.Action.PowerShell.CustomPowerShellVersion, "6");
            
            var pathToPowerShell = CreatePowerShellCoreBootstrapper(mockFileSystem).PathToPowerShellExecutable(variables);
            pathToPowerShell.Should().Be("C:\\Program Files\\PowerShell\\6\\pwsh.exe");
        }
        
        [Test]
        public void IncorrectVersionShouldThrowException()
        {
            var mockFileSystem = Substitute.For<ICalamariFileSystem>();
            SetUpMockFileSystem(mockFileSystem);

            var variables = new CalamariVariableDictionary();
            variables.Add(SpecialVariables.Action.PowerShell.CustomPowerShellVersion, "6-preview");
            
            Action act = () => CreatePowerShellCoreBootstrapper(mockFileSystem).PathToPowerShellExecutable(variables);
            act.Should().Throw<PowerShellVersionNotFoundException>();
        }
        
        [Test]
        public void CustommVersionSpecifiedButNoVersionInstalledShouldThrowException()
        {
            var mockFileSystem = Substitute.For<ICalamariFileSystem>();
            string[] installedPSVersions = new string[] { };
            SetUpMockFileSystem(mockFileSystem, installedPSVersions);

            var variables = new CalamariVariableDictionary();
            variables.Add(SpecialVariables.Action.PowerShell.CustomPowerShellVersion, "6");
            
            Action act = () => CreatePowerShellCoreBootstrapper(mockFileSystem).PathToPowerShellExecutable(variables);
            act.Should().Throw<PowerShellVersionNotFoundException>();
        }
        
        static void SetUpMockFileSystem(ICalamariFileSystem mockFileSystem, string[] powerShellVersions = null)
        {
            string parentDirectoryPath = "C:\\Program Files\\PowerShell";
            var versions = powerShellVersions ?? new[] {"6", "7-preview", "7", "8-preview"};
            
            string[] powerShellPaths = versions.Select(version => $"{parentDirectoryPath}\\{version}").ToArray();
               
            mockFileSystem.EnumerateDirectories(parentDirectoryPath).Returns(powerShellPaths);
            
            mockFileSystem.DirectoryExists(parentDirectoryPath).Returns(true);
            
            for (int i = 0; i < powerShellPaths.Length; i++) 
                mockFileSystem.GetDirectoryName(powerShellPaths[i]).Returns(versions[i]);

            foreach (var t in powerShellPaths)
                mockFileSystem.FileExists($"{t}\\pwsh.exe").Returns(true);
        }

        static PowerShellCoreBootstrapper CreatePowerShellCoreBootstrapper(ICalamariFileSystem mockFileSystem)
        { 
            return new PowerShellCoreBootstrapper(mockFileSystem);
        }
    }
}