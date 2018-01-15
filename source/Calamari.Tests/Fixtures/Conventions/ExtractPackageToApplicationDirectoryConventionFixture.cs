using System.IO;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Tests.Fixtures.Util;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;
using Octopus.Versioning.Metadata;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class ExtractPackageToApplicationDirectoryConventionFixture
    {
        IPackageExtractor extractor;
        CalamariVariableDictionary variables;
        ExtractPackageToApplicationDirectoryConvention convention;
        ICalamariFileSystem fileSystem;
        static readonly string PackageLocation = TestEnvironment.ConstructRootedPath("Package.nupkg");

        [SetUp]
        public void SetUp()
        {
            extractor = Substitute.For<IPackageExtractor>();
            extractor.GetMetadata(PackageLocation).Returns(new PackageMetadata { PackageId = "Acme.Web", Version = "1.0.0" });

            fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.RemoveInvalidFileNameChars(Arg.Any<string>()).Returns(c => c.Arg<string>().Replace("!", ""));

            variables = new CalamariVariableDictionary();
            convention = new ExtractPackageToApplicationDirectoryConvention(extractor, fileSystem);
        }

        [Test]
        public void ShouldPrefixAppPathIfSet()
        {
            var rootPath = TestEnvironment.ConstructRootedPath("MyApp");
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", rootPath);

            convention.Install(new RunningDeployment(PackageLocation, variables));

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Is.EqualTo(Path.Combine(rootPath, "Acme.Web", "1.0.0")));
        }

        [Test]
        public void ShouldPrefixSystemDriveIfPathVariableUnavailable()
        {
            var expectedRoot = string.Format("X:{0}Applications", Path.DirectorySeparatorChar);
            variables.Set("env:SystemDrive", "X:");
            variables.Set("env:HOME", "SomethingElse");
            
            convention.Install(new RunningDeployment(PackageLocation, variables));

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Is.EqualTo(Path.Combine(expectedRoot, "Acme.Web", "1.0.0")));
        }

        [Test]
        public void ShouldPrefixHomeDriveIfOnlyVariableAvailable()
        {
            var variable = string.Format("{0}home{0}MyUser", Path.DirectorySeparatorChar);
            var expectedRoot = string.Format("{0}{1}Applications", variable, Path.DirectorySeparatorChar);
            variables.Set("env:HOME", variable);
            
            convention.Install(new RunningDeployment(PackageLocation, variables));

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Is.EqualTo(Path.Combine(expectedRoot, "Acme.Web", "1.0.0")));
        }

        [Test]
        [ExpectedException]
        public void ShouldThrowExceptionIfNoPathUnresolved()
        {
            convention.Install(new RunningDeployment(PackageLocation, variables));
        }


        [Test]
        public void ShouldExtractToVersionedFolderWithDefaultPath()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", TestEnvironment.ConstructRootedPath());
            
            convention.Install(new RunningDeployment(PackageLocation, variables));

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Does.EndWith(Path.Combine("Acme.Web", "1.0.0")));
        }


        [Test]
        public void ShouldAppendToVersionedFolderIfAlreadyExisting()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", TestEnvironment.ConstructRootedPath());

            fileSystem.DirectoryExists(Arg.Is<string>(path => path.EndsWith(Path.Combine("Acme.Web", "1.0.0")))).Returns(true);
            fileSystem.DirectoryExists(Arg.Is<string>(path => path.EndsWith(Path.Combine("Acme.Web", "1.0.0_1")))).Returns(true);
            fileSystem.DirectoryExists(Arg.Is<string>(path => path.EndsWith(Path.Combine("Acme.Web", "1.0.0_2")))).Returns(true);


            convention.Install(new RunningDeployment(PackageLocation, variables));

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Does.EndWith(Path.Combine("Acme.Web", "1.0.0_3")));
            
        }
        

        [Test]
        public void ShouldExtractToEnvironmentSpecificFolderIfProvided()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", TestEnvironment.ConstructRootedPath());
            variables.Set("Octopus.Environment.Name", "Production");

            convention.Install(new RunningDeployment(PackageLocation, variables));

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Does.EndWith(Path.Combine("Production","Acme.Web","1.0.0")));
        }

        [Test]
        public void ShouldExtractToTenantSpecificFolderIfProvided()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", TestEnvironment.ConstructRootedPath());
            variables.Set("Octopus.Environment.Name", "Production");
            variables.Set("Octopus.Deployment.Tenant.Name", "MegaCorp");

            convention.Install(new RunningDeployment(PackageLocation, variables));

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Does.EndWith(Path.Combine("MegaCorp", "Production", "Acme.Web", "1.0.0")));
        }


        [Test]
        public void ShouldRemoveInvalidPathCharsFromEnvironmentName()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", TestEnvironment.ConstructRootedPath());
            variables.Set("Octopus.Environment.Name", "Production! Tokyo");

            convention.Install(new RunningDeployment(PackageLocation, variables));

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Does.EndWith(Path.Combine("Production Tokyo","Acme.Web","1.0.0")));
        }

       
    }
}