using System.IO;
using Calamari.Commands;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Calamari.Tests.Fixtures.Util;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class ExtractPackageToApplicationDirectoryConventionFixture
    {
        IPackageExtractor extractor;
        CalamariVariableDictionary variables;
        ExtractPackageToApplicationDirectoryConvention convention;
        ICalamariFileSystem fileSystem;
        static readonly string PackageLocation = TestEnvironment.ConstructRootedPath("Acme.Web.1.0.0.zip");
        private IExecutionContext executionContext;
        
        [SetUp]
        public void SetUp()
        {
            extractor = Substitute.For<IPackageExtractor>();

            fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.RemoveInvalidFileNameChars(Arg.Any<string>()).Returns(c => c.Arg<string>().Replace("!", ""));

            variables = new CalamariVariableDictionary();
            convention = new ExtractPackageToApplicationDirectoryConvention(extractor, fileSystem, new LogWrapper());
            executionContext = new CalamariExecutionContext(PackageLocation, variables);
        }

        [Test]
        public void ShouldPrefixAppPathIfSet()
        {
            var rootPath = TestEnvironment.ConstructRootedPath("MyApp");
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", rootPath);

            convention.Run(executionContext);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Is.EqualTo(Path.Combine(rootPath, "Acme.Web", "1.0.0")));
        }

        [Test]
        public void ShouldPrefixSystemDriveIfPathVariableUnavailable()
        {
            var expectedRoot = string.Format("X:{0}Applications", Path.DirectorySeparatorChar);
            variables.Set("env:SystemDrive", "X:");
            variables.Set("env:HOME", "SomethingElse");
            
            convention.Run(executionContext);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Is.EqualTo(Path.Combine(expectedRoot, "Acme.Web", "1.0.0")));
        }

        [Test]
        public void ShouldPrefixHomeDriveIfOnlyVariableAvailable()
        {
            var variable = string.Format("{0}home{0}MyUser", Path.DirectorySeparatorChar);
            var expectedRoot = string.Format("{0}{1}Applications", variable, Path.DirectorySeparatorChar);
            variables.Set("env:HOME", variable);
            
            convention.Run(executionContext);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Is.EqualTo(Path.Combine(expectedRoot, "Acme.Web", "1.0.0")));
        }

        [Test]
        [ExpectedException]
        public void ShouldThrowExceptionIfNoPathUnresolved()
        {
            convention.Run(executionContext);
        }


        [Test]
        public void ShouldExtractToVersionedFolderWithDefaultPath()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", TestEnvironment.ConstructRootedPath());
            
            convention.Run(executionContext);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Does.EndWith(Path.Combine("Acme.Web", "1.0.0")));
        }


        [Test]
        public void ShouldAppendToVersionedFolderIfAlreadyExisting()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", TestEnvironment.ConstructRootedPath());

            fileSystem.DirectoryExists(Arg.Is<string>(path => path.EndsWith(Path.Combine("Acme.Web", "1.0.0")))).Returns(true);
            fileSystem.DirectoryExists(Arg.Is<string>(path => path.EndsWith(Path.Combine("Acme.Web", "1.0.0_1")))).Returns(true);
            fileSystem.DirectoryExists(Arg.Is<string>(path => path.EndsWith(Path.Combine("Acme.Web", "1.0.0_2")))).Returns(true);


            convention.Run(executionContext);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Does.EndWith(Path.Combine("Acme.Web", "1.0.0_3")));
            
        }
        

        [Test]
        public void ShouldExtractToEnvironmentSpecificFolderIfProvided()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", TestEnvironment.ConstructRootedPath());
            variables.Set("Octopus.Environment.Name", "Production");

            convention.Run(executionContext);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Does.EndWith(Path.Combine("Production","Acme.Web","1.0.0")));
        }

        [Test]
        public void ShouldExtractToTenantSpecificFolderIfProvided()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", TestEnvironment.ConstructRootedPath());
            variables.Set("Octopus.Environment.Name", "Production");
            variables.Set("Octopus.Deployment.Tenant.Name", "MegaCorp");

            convention.Run(executionContext);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Does.EndWith(Path.Combine("MegaCorp", "Production", "Acme.Web", "1.0.0")));
        }


        [Test]
        public void ShouldRemoveInvalidPathCharsFromEnvironmentName()
        {
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", TestEnvironment.ConstructRootedPath());
            variables.Set("Octopus.Environment.Name", "Production! Tokyo");

            convention.Run(executionContext);

            Assert.That(variables.Get("OctopusOriginalPackageDirectoryPath"), Does.EndWith(Path.Combine("Production Tokyo","Acme.Web","1.0.0")));
        }

       
    }
}