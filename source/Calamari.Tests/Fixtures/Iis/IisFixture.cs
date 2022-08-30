#if IIS_SUPPORT
using System;
using Calamari.Integration.Iis;
using Calamari.Testing.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Iis
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    public class IisFixture
    {
        readonly WebServerSupport webServer = WebServerSupport.AutoDetect();
        string siteName;

        [SetUp]
        public void SetUp()
        {
            siteName = "Test-" + Guid.NewGuid();
            webServer.CreateWebSiteOrVirtualDirectory(siteName, "/", "C:\\InetPub\\wwwroot", 1081);
            webServer.CreateWebSiteOrVirtualDirectory(siteName, "/Foo", "C:\\InetPub\\wwwroot", 1081);
            webServer.CreateWebSiteOrVirtualDirectory(siteName, "/Foo/Bar/Baz", "C:\\InetPub\\wwwroot", 1081);
        }

        [Test]
        public void CanUpdateIisSite()
        {
            var server = new InternetInformationServer();
            var success = server.OverwriteHomeDirectory(siteName, "C:\\Windows\\system32", false);
            Assert.IsTrue(success, "Home directory was not overwritten");

            var path = webServer.GetHomeDirectory(siteName, "/");
            Assert.AreEqual("C:\\Windows\\system32", path);
        }

        [Test]
        public void CanUpdateIisSiteWithVirtualDirectory()
        {
            var server = new InternetInformationServer();
            var success = server.OverwriteHomeDirectory(siteName + "/Foo", "C:\\Windows\\Microsoft.NET", false);
            Assert.IsTrue(success, "Home directory was not overwritten");

            var path = webServer.GetHomeDirectory(siteName, "/Foo");
            Assert.AreEqual("C:\\Windows\\Microsoft.NET", path);
        }

        [Test]
        public void CanUpdateIisSiteWithNestedVirtualDirectory()
        {
            var server = new InternetInformationServer();
            var success = server.OverwriteHomeDirectory(siteName + "/Foo/Bar/Baz", "C:\\Windows\\Microsoft.NET\\Framework", false);
            Assert.IsTrue(success, "Home directory was not overwritten");

            var path = webServer.GetHomeDirectory(siteName, "/Foo/Bar/Baz");
            Assert.AreEqual("C:\\Windows\\Microsoft.NET\\Framework", path);
        }

        [TearDown]
        public void TearDown()
        {
            webServer.DeleteWebSite(siteName);
        }
    }
}
#endif