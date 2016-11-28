using System.IO;
using Calamari.Extensibility.IIS.FileSystem;
using Calamari.Extensibility.TestingUtilities;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Extensibility.IIS.Tests
{
    [TestFixture]
    public class IISDeploymentFeatureFixture
    {
        ICalamariFileSystem fileSystem;
        IInternetInformationServer iis;
        IVariableDictionary variables;
        const string stagingDirectory = "C:\\Applications\\Acme\\1.0.0";

        [SetUp]
        public void SetUp()
        {
            variables = new TestVariableDictionary();
            fileSystem = Substitute.For<ICalamariFileSystem>();
            iis = Substitute.For<IInternetInformationServer>();
        }
       
        [Test]
        public void ShouldUpdatePath()
        {
            const string websiteName = "AcmeOnline";
            variables.Set(SpecialVariables.Package.UpdateIisWebsite, true.ToString());
            variables.Set(SpecialVariables.Package.UpdateIisWebsiteName, websiteName);
            fileSystem.FileExists(Path.Combine(stagingDirectory, "Web.config")).Returns(true);
            iis.OverwriteHomeDirectory(websiteName, stagingDirectory, false).Returns(true);

            var feature = new IISDeploymentFeature(fileSystem, iis, Substitute.For<ILog>());
            feature.AfterDeploy2(variables, stagingDirectory);

            iis.Received().OverwriteHomeDirectory(websiteName, stagingDirectory, false);
        }
 
        [Test]
        public void ShouldUsePackageNameIfWebsiteNameVariableNotSupplied()
        {
            const string packageId = "Acme.1.1.1";
            variables.Set(SpecialVariables.Package.UpdateIisWebsite, true.ToString());
            variables.Set(SpecialVariables.Package.NuGetPackageId, packageId);
            fileSystem.FileExists(Path.Combine(stagingDirectory, "Web.config")).Returns(true);
            iis.OverwriteHomeDirectory(packageId, stagingDirectory, false).Returns(true);

            var feature = new IISDeploymentFeature(fileSystem, iis, Substitute.For<ILog>());
            feature.AfterDeploy2(variables, stagingDirectory);

            iis.Received().OverwriteHomeDirectory(packageId, stagingDirectory, false);
        }
        
        [Test]
        public void ShouldForceIis6CompatibilityIfFlagSet()
        {
            const string websiteName = "AcmeOnline";
            variables.Set(SpecialVariables.Package.UpdateIisWebsite, true.ToString());
            variables.Set(SpecialVariables.Package.UpdateIisWebsiteName, websiteName);
            variables.Set(SpecialVariables.UseLegacyIisSupport, true.ToString());
            fileSystem.FileExists(Path.Combine(stagingDirectory, "Web.config")).Returns(true);
            iis.OverwriteHomeDirectory(websiteName, stagingDirectory, true).Returns(true);

            var feature = new IISDeploymentFeature(fileSystem, iis, Substitute.For<ILog>());
            feature.AfterDeploy2(variables, stagingDirectory);

            iis.Received().OverwriteHomeDirectory(websiteName, stagingDirectory, true);
        }

        [Test]
        public void ShouldNotUpdatePathIfFlagNotSet()
        {
            const string websiteName = "AcmeOnline";
            variables.Set(SpecialVariables.Package.UpdateIisWebsite, false.ToString());
            variables.Set(SpecialVariables.Package.UpdateIisWebsiteName, websiteName);
            fileSystem.FileExists(Path.Combine(stagingDirectory, "Web.config")).Returns(true);
            iis.OverwriteHomeDirectory(websiteName, stagingDirectory, false).Returns(true);

            var feature = new IISDeploymentFeature(fileSystem, iis, Substitute.For<ILog>());
            feature.AfterDeploy2(variables, stagingDirectory);

            iis.DidNotReceive().OverwriteHomeDirectory(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
        }        
    }
}