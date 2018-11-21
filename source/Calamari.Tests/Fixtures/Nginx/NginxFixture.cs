using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assent;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Nginx;
using Calamari.Tests.Helpers;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Nginx
{
    [TestFixture]
    [Category(TestEnvironment.CompatibleOS.Nix)]
    [Category(TestEnvironment.CompatibleOS.Mac)]
    public class NginxFixture
    {
        readonly ICalamariFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
        private NginxServer nginxServer;
        string tempDirectory;

        readonly string httpOnlyBinding =
            "[{\"protocol\":\"http\",\"port\":\"80\",\"ipAddress\":\"*\",\"certificateLocation\":null,\"certificateKeyLocation\":null,\"securityProtocols\":null,\"enabled\":true}]";

        readonly string httpAndHttpsBindings = "[{\"protocol\":\"http\",\"port\":\"80\",\"ipAddress\":\"*\",\"certificateLocation\":null,\"certificateKeyLocation\":null,\"securityProtocols\":null,\"enabled\":true},{\"protocol\":\"https\",\"port\":\"443\",\"ipAddress\":\"*\",\"certificateLocation\":\"/etc/ssl/nginxsample/certificate.crt\",\"certificateKeyLocation\":\"/etc/ssl/nginxsample/certificate.key\",\"securityProtocols\":null,\"enabled\":true}]";
        readonly string httpAndHttpsBindingWithCertificateVariable = "[{\"protocol\":\"http\",\"port\":\"80\",\"ipAddress\":\"*\",\"certificateLocation\":null,\"certificateKeyLocation\":null,\"securityProtocols\":null,\"enabled\":true},{\"protocol\":\"https\",\"port\":\"443\",\"ipAddress\":\"*\",\"certificateLocation\":null,\"certificateKeyLocation\":null,\"securityProtocols\":null,\"enabled\":true,\"thumbprint\":null,\"certificateVariable\":\"NginxSampleWebAppCertificate\"}]";

        [SetUp]
        public void SetUp()
        {
            tempDirectory = fileSystem.CreateTemporaryDirectory();
            nginxServer = Substitute.For<NginxServer>();
            nginxServer.GetConfigRootDirectory().Returns("/etc/nginx/conf.d");
            nginxServer.GetSslRootDirectory().Returns("/etc/ssl");
        }

        [TearDown]
        public void TearDown()
        {
            if (fileSystem.DirectoryExists(tempDirectory))
                fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);
        }

        [Test]
        public void SetupStaticContentSite()
        {
            var locations =
                JsonConvert.DeserializeObject<IEnumerable<Location>>(
                    "[{\"path\":\"/\",\"directives\":\"{\\\"root\\\":\\\"#{Octopus.Action.Package.InstallationDirectoryPath}/wwwroot\\\",\\\"try_files\\\":\\\"$uri $uri/ /index.html\\\"}\",\"headers\":\"\",\"reverseProxy\":false,\"reverseProxyUrl\":\"\",\"reverseProxyHeaders\":\"\",\"reverseProxyDirectives\":\"\"}]");
            
            var virtualServerName = "StaticContent";

            nginxServer
                .WithVirtualServerName(virtualServerName)
                .WithServerBindings(JsonConvert.DeserializeObject<IEnumerable<Binding>>(httpOnlyBinding),
                    new Dictionary<string, (string, string, string)>())
                .WithRootLocation(locations.First());

            nginxServer.BuildConfiguration();
            nginxServer.SaveConfiguration(tempDirectory);
            
            var nginxConfigFilePath = Path.Combine(tempDirectory, "conf", $"{virtualServerName}.conf");
            this.Assent(File.ReadAllText(nginxConfigFilePath), TestEnvironment.AssentConfiguration);
        }

        [Test]
        public void SetupReverseProxySite()
        {
            var locations =
                JsonConvert.DeserializeObject<IEnumerable<Location>>(
                    "[{\"path\":\"/\",\"directives\":\"\",\"headers\":\"\",\"reverseProxy\":\"True\",\"reverseProxyUrl\":\"http://localhost:5000\",\"reverseProxyHeaders\":\"{\\\"Upgrade\\\":\\\"$http_upgrade\\\",\\\"Connection\\\":\\\"keep-alive\\\",\\\"Host\\\":\\\"$host\\\",\\\"X-Forwarded-For\\\":\\\"$proxy_add_x_forwarded_for\\\",\\\"X-Forwarded-Proto\\\":\\\"$scheme\\\"}\",\"reverseProxyDirectives\":\"{\\\"proxy_http_version\\\":\\\"1.1\\\",\\\"proxy_cache_bypass\\\":\\\"$http_upgrade\\\"}\"}]");
            
            var virtualServerName = "ReverseProxy";

            nginxServer
                .WithVirtualServerName(virtualServerName)
                .WithServerBindings(JsonConvert.DeserializeObject<IEnumerable<Binding>>(httpOnlyBinding),
                    new Dictionary<string, (string, string, string)>())
                .WithRootLocation(locations.First());

            nginxServer.BuildConfiguration();
            nginxServer.SaveConfiguration(tempDirectory);
            
            var nginxConfigFilePath = Path.Combine(tempDirectory, "conf", $"{virtualServerName}.conf");
            this.Assent(File.ReadAllText(nginxConfigFilePath), TestEnvironment.AssentConfiguration);
        }

        [Test]
        public void SetupStaticContentWithReverseProxySite()
        {
            var locations =
                JsonConvert.DeserializeObject<IEnumerable<Location>>(
                    "[{\"path\":\"/\",\"directives\":\"{\\\"root\\\":\\\"#{Octopus.Action.Package.InstallationDirectoryPath}/wwwroot\\\",\\\"try_files\\\":\\\"$uri $uri/ /index.html\\\"}\",\"headers\":\"\",\"reverseProxy\":false,\"reverseProxyUrl\":\"\",\"reverseProxyHeaders\":\"\",\"reverseProxyDirectives\":\"\"},{\"path\":\"/api/\",\"directives\":\"\",\"headers\":\"\",\"reverseProxy\":\"True\",\"reverseProxyUrl\":\"http://localhost:5000\",\"reverseProxyHeaders\":\"{\\\"Upgrade\\\":\\\"$http_upgrade\\\",\\\"Connection\\\":\\\"keep-alive\\\",\\\"Host\\\":\\\"$host\\\",\\\"X-Forwarded-For\\\":\\\"$proxy_add_x_forwarded_for\\\",\\\"X-Forwarded-Proto\\\":\\\"$scheme\\\"}\",\"reverseProxyDirectives\":\"{\\\"proxy_http_version\\\":\\\"1.1\\\",\\\"proxy_cache_bypass\\\":\\\"$http_upgrade\\\"}\"}]"
                ).ToList();
            
            var virtualServerName = "StaticContent";
            var rootLocation = locations.First();
            var apiLocation = locations.Last();

            nginxServer
                .WithVirtualServerName(virtualServerName)
                .WithServerBindings(JsonConvert.DeserializeObject<IEnumerable<Binding>>(httpOnlyBinding),
                    new Dictionary<string, (string, string, string)>())
                .WithRootLocation(rootLocation)
                .WithAdditionalLocations(new []{apiLocation});

            nginxServer.BuildConfiguration();
            nginxServer.SaveConfiguration(tempDirectory);
            
            var rootConfigFilePath = Path.Combine(tempDirectory, "conf", $"{virtualServerName}.conf");
            this.Assent(
                File.ReadAllText(rootConfigFilePath), 
                TestEnvironment.AssentConfiguration,
                $"{nameof(SetupStaticContentWithReverseProxySite)}.rootLocation"
            );

            var apiConfigFilePath = Path.Combine(
                tempDirectory, 
                "conf", 
                $"{virtualServerName}.conf.d",
                $"location.{apiLocation.Path.Trim('/')}.conf"
            );
            
            this.Assent(
                File.ReadAllText(apiConfigFilePath), 
                TestEnvironment.AssentConfiguration,
                $"{nameof(SetupStaticContentWithReverseProxySite)}.apiLocation"
            );
        }

        [Test]
        public void SetupReverseProxyWithSslSite()
        {
            var locations =
                JsonConvert.DeserializeObject<IEnumerable<Location>>(
                    "[{\"path\":\"/\",\"directives\":\"\",\"headers\":\"\",\"reverseProxy\":\"True\",\"reverseProxyUrl\":\"http://localhost:5000\",\"reverseProxyHeaders\":\"{\\\"Upgrade\\\":\\\"$http_upgrade\\\",\\\"Connection\\\":\\\"keep-alive\\\",\\\"Host\\\":\\\"$host\\\",\\\"X-Forwarded-For\\\":\\\"$proxy_add_x_forwarded_for\\\",\\\"X-Forwarded-Proto\\\":\\\"$scheme\\\"}\",\"reverseProxyDirectives\":\"{\\\"proxy_http_version\\\":\\\"1.1\\\",\\\"proxy_cache_bypass\\\":\\\"$http_upgrade\\\"}\"}]"
                );
            
            var virtualServerName = "HttpsReverseProxy";

            nginxServer
                .WithVirtualServerName(virtualServerName)
                .WithServerBindings(JsonConvert.DeserializeObject<IEnumerable<Binding>>(httpAndHttpsBindings),
                    new Dictionary<string, (string, string, string)>())
                .WithRootLocation(locations.First());

            nginxServer.BuildConfiguration();
            nginxServer.SaveConfiguration(tempDirectory);
            
            var nginxConfigFilePath = Path.Combine(tempDirectory, "conf", $"{virtualServerName}.conf");
            this.Assent(File.ReadAllText(nginxConfigFilePath), TestEnvironment.AssentConfiguration);
        }

        [Test]
        public void SetupReverseProxyWithSslUsingCertificateVariableSite()
        {
            var locations =
                JsonConvert.DeserializeObject<IEnumerable<Location>>(
                    "[{\"path\":\"/\",\"directives\":\"\",\"headers\":\"\",\"reverseProxy\":\"True\",\"reverseProxyUrl\":\"http://localhost:5000\",\"reverseProxyHeaders\":\"{\\\"Upgrade\\\":\\\"$http_upgrade\\\",\\\"Connection\\\":\\\"keep-alive\\\",\\\"Host\\\":\\\"$host\\\",\\\"X-Forwarded-For\\\":\\\"$proxy_add_x_forwarded_for\\\",\\\"X-Forwarded-Proto\\\":\\\"$scheme\\\"}\",\"reverseProxyDirectives\":\"{\\\"proxy_http_version\\\":\\\"1.1\\\",\\\"proxy_cache_bypass\\\":\\\"$http_upgrade\\\"}\"}]"
                );
            
            var virtualServerName = "HttpsReverseProxy";
            var certificates = new Dictionary<string, (string, string, string)>{
                {"NginxSampleWebAppCertificate", ("www.nginxsamplewebapp.com", "", "")}
            };

            nginxServer
                .WithVirtualServerName(virtualServerName)
                .WithServerBindings(JsonConvert.DeserializeObject<IEnumerable<Binding>>(httpAndHttpsBindingWithCertificateVariable),
                    certificates)
                .WithRootLocation(locations.First());

            nginxServer.BuildConfiguration();
            nginxServer.SaveConfiguration(tempDirectory);
            
            var nginxConfigFilePath = Path.Combine(tempDirectory, "conf", $"{virtualServerName}.conf");
            this.Assent(File.ReadAllText(nginxConfigFilePath), TestEnvironment.AssentConfiguration);
        }
    }
}