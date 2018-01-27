using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Calamari.Integration.Packages.Download;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octopus.Versioning.Factories;
using Octopus.Versioning.Maven;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class MavenPackageDownloaderFixture
    {
        static readonly string TentacleHome = TestEnvironment.GetTestPath("Fixtures", "PackageDownload");

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            Environment.SetEnvironmentVariable("TentacleHome", TentacleHome);
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            Environment.SetEnvironmentVariable("TentacleHome", null);
        }

        [Test]
        public void Blah()
        {
            var d = new MavenPackageDownloader();
            var result = d.DownloadPackage("com.google.guava:guava", VersionFactory.CreateMavenVersion("22.0"), "feed-maven",
                new Uri("https://repo.maven.apache.org/maven2/"), new NetworkCredential("", ""), true, 3, TimeSpan.FromSeconds(3));
        }
    }
}
