using System;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    public class DeployPackageFixture : CalamariFixture
    {
        [Test]
        public void ShouldDeployPackage()
        {
            var variables = new VariableDictionary();
            var result = DeployPackage("Acme.Web", variables);
            result.AssertZero();
        }

        CalamariResult DeployPackage(string packageName, VariableDictionary variables)
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageName, "1.0.0")))
            {
                variables.Save(variablesFile.FilePath);

                return Invoke(Calamari()
                    .Action("deploy-package")
                    .Argument("package", acmeWeb.FilePath)
                    .Argument("variables", variablesFile.FilePath));
            }
        }
    }
}
