using Calamari.Common.Plumbing.FileSystem;
using Calamari.Integration.FileSystem;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
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