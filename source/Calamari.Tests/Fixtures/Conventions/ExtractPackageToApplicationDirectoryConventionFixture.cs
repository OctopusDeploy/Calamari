using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using NSubstitute;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class ExtractPackageToApplicationDirectoryConventionFixture
    {
        IPackageExtractor extractor;
        VariableDictionary variables;
        ExtractPackageToApplicationDirectoryConvention convention;
        ICalamariFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            extractor = Substitute.For<IPackageExtractor>();
            extractor.GetMetadata("C:\\Package.nupkg").Returns(new PackageMetadata { Id = "Acme.Web", Version = "1.0.0" });

            fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.RemoveInvalidFileNameChars(Arg.Any<string>()).Returns(c => new CalamariPhysicalFileSystem().RemoveInvalidFileNameChars(c.Arg<string>()));

            variables = new VariableDictionary();
            variables.Set("env:SystemDrive", "C:");

            convention = new ExtractPackageToApplicationDirectoryConvention(extractor, fileSystem, new SystemSemaphore());
        }

        [Test]
        public void ShouldExtractToVersionedFolderWithDefaultPath()
        {
            convention.Install(new RunningDeployment("C:\\Package.nupkg", variables));
            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Is.EqualTo("C:\\Applications\\Acme.Web\\1.0.0"));
        }

        [Test]
        public void ShouldAppendToVersionedFolderIfAlreadyExisting()
        {
            fileSystem.DirectoryExists("C:\\Applications\\Acme.Web\\1.0.0").Returns(true);
            fileSystem.DirectoryExists("C:\\Applications\\Acme.Web\\1.0.0_1").Returns(true);
            fileSystem.DirectoryExists("C:\\Applications\\Acme.Web\\1.0.0_2").Returns(true);

            convention.Install(new RunningDeployment("C:\\Package.nupkg", variables));

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Is.EqualTo("C:\\Applications\\Acme.Web\\1.0.0_3"));
        }

        [Test]
        public void ShouldExtractToEnvironmentSpecificFolderIfProvided()
        {
            variables.Set("Octopus.Environment.Name", "Production");

            convention.Install(new RunningDeployment("C:\\Package.nupkg", variables));

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Is.EqualTo("C:\\Applications\\Production\\Acme.Web\\1.0.0"));
        }

        [Test]
        public void ShouldRemoveInvalidPathCharsFromEnvironmentName()
        {
            variables.Set("Octopus.Environment.Name", "Production: Tokyo");

            convention.Install(new RunningDeployment("C:\\Package.nupkg", variables));

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Is.EqualTo("C:\\Applications\\Production Tokyo\\Acme.Web\\1.0.0"));
        }

        [Test]
        public void ShouldPreferAppDirPathIfSet()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", "C:\\MyApps");
            convention.Install(new RunningDeployment("C:\\Package.nupkg", variables));
            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Is.EqualTo("C:\\MyApps\\Acme.Web\\1.0.0"));
        }

        [Test]
        public void ShouldPreferAppDirEnvironmentVariablePathIfSet()
        {
            variables.Set("env:Octopus.Tentacle.Agent.ApplicationDirectoryPath", "C:\\MyApps");
            convention.Install(new RunningDeployment("C:\\Package.nupkg", variables));
            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Is.EqualTo("C:\\MyApps\\Acme.Web\\1.0.0"));
        }
    }
}