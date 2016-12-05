using Calamari.Integration.FileSystem;
using Calamari.IntegrationTests.Fixtures.Deployment.Packages;
using Calamari.IntegrationTests.Helpers;
using NUnit.Framework;

namespace Calamari.IntegrationTests.Fixtures.Deployment
{
    [TestFixture]
    [Category(TestEnvironment.CompatibleOS.Windows)]
    public class DeployPackageWithScriptsFixture : DeployPackageFixture
    {
        private const string ServiceName = "Acme.Package";

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
        }

        [Test]
        public void ShouldSkipRemainingConventions()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, "1.0.0")))
            {
                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                //Post Deploy should not run.
                result.AssertNoOutput("hello from post-deploy");
            }
        }

        [TearDown]
        public override void CleanUp()
        {
            base.CleanUp();
        }
    }
}