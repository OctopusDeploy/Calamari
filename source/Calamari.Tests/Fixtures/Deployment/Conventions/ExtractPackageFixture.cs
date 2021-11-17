using System;
using System.IO;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Fixtures.Util;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Conventions
{
    [TestFixture]
    public class ExtractPackageFixture
    {
        IVariables variables;
        ExtractPackage extractPackage;
        ICalamariFileSystem fileSystem;
        static readonly PathToPackage PathToPackage = new PathToPackage(TestEnvironment.ConstructRootedPath("Acme.Web.1.0.0.zip"));

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.RemoveInvalidFileNameChars(Arg.Any<string>()).Returns(c => c.Arg<string>().Replace("!", ""));

            variables = new CalamariVariables();
            extractPackage = new ExtractPackage(Substitute.For<ICombinedPackageExtractor>(), fileSystem, variables, new InMemoryLog());
        }

        [Test]
        public void ShouldPrefixAppPathIfSet()
        {
            var rootPath = TestEnvironment.ConstructRootedPath("MyApp");
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", rootPath);

            extractPackage.ExtractToApplicationDirectory(PathToPackage);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Is.EqualTo(Path.Combine(rootPath, "Acme.Web", "1.0.0")));
        }

        [Test]
        public void ShouldPrefixSystemDriveIfPathVariableUnavailable()
        {
            var expectedRoot = string.Format("X:{0}Applications", Path.DirectorySeparatorChar);
            variables.Set("env:SystemDrive", "X:");
            variables.Set("env:HOME", "SomethingElse");
            
            extractPackage.ExtractToApplicationDirectory(PathToPackage);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Is.EqualTo(Path.Combine(expectedRoot, "Acme.Web", "1.0.0")));
        }

        [Test]
        public void ShouldPrefixHomeDriveIfOnlyVariableAvailable()
        {
            var variable = string.Format("{0}home{0}MyUser", Path.DirectorySeparatorChar);
            var expectedRoot = string.Format("{0}{1}Applications", variable, Path.DirectorySeparatorChar);
            variables.Set("env:HOME", variable);
            
            extractPackage.ExtractToApplicationDirectory(PathToPackage);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Is.EqualTo(Path.Combine(expectedRoot, "Acme.Web", "1.0.0")));
        }

        [Test]
        [ExpectedException]
        public void ShouldThrowExceptionIfNoPathUnresolved()
        {
            extractPackage.ExtractToApplicationDirectory(PathToPackage);
        }


        [Test]
        public void ShouldExtractToVersionedFolderWithDefaultPath()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", TestEnvironment.ConstructRootedPath());
            
            extractPackage.ExtractToApplicationDirectory(PathToPackage);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Does.EndWith(Path.Combine("Acme.Web", "1.0.0")));
        }


        [Test]
        public void ShouldAppendToVersionedFolderIfAlreadyExisting()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", TestEnvironment.ConstructRootedPath());

            fileSystem.DirectoryExists(Arg.Is<string>(path => path.EndsWith(Path.Combine("Acme.Web", "1.0.0")))).Returns(true);
            fileSystem.DirectoryExists(Arg.Is<string>(path => path.EndsWith(Path.Combine("Acme.Web", "1.0.0_1")))).Returns(true);
            fileSystem.DirectoryExists(Arg.Is<string>(path => path.EndsWith(Path.Combine("Acme.Web", "1.0.0_2")))).Returns(true);


            extractPackage.ExtractToApplicationDirectory(PathToPackage);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Does.EndWith(Path.Combine("Acme.Web", "1.0.0_3")));
            
        }
        

        [Test]
        public void ShouldExtractToEnvironmentSpecificFolderIfProvided()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", TestEnvironment.ConstructRootedPath());
            variables.Set("Octopus.Environment.Name", "Production");

            extractPackage.ExtractToApplicationDirectory(PathToPackage);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Does.EndWith(Path.Combine("Production","Acme.Web","1.0.0")));
        }

        [Test]
        public void ShouldExtractToTenantSpecificFolderIfProvided()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", TestEnvironment.ConstructRootedPath());
            variables.Set("Octopus.Environment.Name", "Production");
            variables.Set("Octopus.Deployment.Tenant.Name", "MegaCorp");

            extractPackage.ExtractToApplicationDirectory(PathToPackage);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Does.EndWith(Path.Combine("MegaCorp", "Production", "Acme.Web", "1.0.0")));
        }


        [Test]
        public void ShouldRemoveInvalidPathCharsFromEnvironmentName()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", TestEnvironment.ConstructRootedPath());
            variables.Set("Octopus.Environment.Name", "Production! Tokyo");

            extractPackage.ExtractToApplicationDirectory(PathToPackage);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Does.EndWith(Path.Combine("Production Tokyo","Acme.Web","1.0.0")));
        }

       
    }
}