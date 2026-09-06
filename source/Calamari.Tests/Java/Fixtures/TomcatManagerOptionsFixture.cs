using Calamari.Integration.Tomcat;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Java.Fixtures
{
    [TestFixture]
    public class TomcatManagerOptionsFixture
    {
        [Test]
        public void DeployUrl_WithNameAndVersion_IncludesUpdateFlagPathAndVersion()
        {
            var options = new TomcatManagerOptions("http://localhost:8080/manager", "admin", "pw", "", "myapp", "", "1.0");

            options.DeployUrl.Should().Be("http://localhost:8080/manager/text/deploy?update=true&version=1.0&path=/myapp");
        }

        [Test]
        public void RedeployUrl_DoesNotIncludeUpdateFlag()
        {
            var options = new TomcatManagerOptions("http://localhost:8080/manager", "admin", "pw", "", "myapp", "sometag", "");

            options.RedeployUrl.Should().Be("http://localhost:8080/manager/text/deploy?path=/myapp&tag=sometag");
        }

        [Test]
        public void UrlPath_WhenNameIsRootSlash_IsEmpty()
        {
            var options = new TomcatManagerOptions("http://localhost:8080/manager", "admin", "pw", "", "/", "", "");

            options.UrlPath.Should().Be("");
        }

        [Test]
        public void UrlPath_WhenNameHasLeadingSlashes_StripsThem()
        {
            var options = new TomcatManagerOptions("http://localhost:8080/manager", "admin", "pw", "", "///myapp", "", "");

            options.UrlPath.Should().Be("myapp");
        }

        [Test]
        public void UrlPath_WhenNameIsBlank_DerivesFromApplicationFilename()
        {
            var options = new TomcatManagerOptions("http://localhost:8080/manager", "admin", "pw", "/tmp/staging/myapp#sub##2.0.war", "", "", "");

            options.UrlPath.Should().Be("myapp/sub");
        }

        [Test]
        public void UrlPath_WhenNeitherNameNorApplicationIsSet_IsNull()
        {
            var options = new TomcatManagerOptions("http://localhost:8080/manager", "admin", "pw", "", "", "", "");

            options.UrlPath.Should().BeNull();
        }

        [Test]
        public void StopUrl_WithNoNameOrVersion_HasNoPathOrVersionParam()
        {
            var options = new TomcatManagerOptions("http://localhost:8080/manager", "admin", "pw", "", "", "", "");

            options.StopUrl.Should().Be("http://localhost:8080/manager/text/stop");
        }

        [Test]
        public void ListUrl_IsAlwaysJustTheListEndpoint()
        {
            var options = new TomcatManagerOptions("http://localhost:8080/manager", "admin", "pw", "", "myapp", "", "1.0");

            options.ListUrl.Should().Be("http://localhost:8080/manager/text/list");
        }

        [Test]
        public void Controller_WhenBlank_DefaultsToLocalhost8080()
        {
            var options = new TomcatManagerOptions("", "admin", "pw", "", "", "", "");

            options.Controller.Should().Be("http://localhost:8080");
        }

        [Test]
        public void AuthorizationHeaderValue_IsBasicBase64OfUserColonPassword()
        {
            var options = new TomcatManagerOptions("http://localhost:8080/manager", "admin", "pw", "", "", "", "");

            options.AuthorizationHeaderValue.Should().Be("Basic YWRtaW46cHc=");
        }
    }
}
