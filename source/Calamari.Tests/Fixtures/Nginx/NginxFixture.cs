using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assent;
using Calamari.Deployment;
using Calamari.Deployment.Features;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Nginx;
using Calamari.Integration.Processes;
using Calamari.Tests.Helpers;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Nginx
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
    public class NginxFixture : CalamariFixture
    {
        readonly ICalamariFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
        private NginxServer nginxServer;
        string tempDirectory;

        readonly string httpOnlyBinding =
            "[{\"protocol\":\"http\",\"port\":\"80\",\"ipAddress\":\"*\",\"certificateLocation\":null,\"certificateKeyLocation\":null,\"securityProtocols\":null,\"enabled\":true}]";

        readonly string httpAndHttpsBindings = "[{\"protocol\":\"http\",\"port\":\"80\",\"ipAddress\":\"*\",\"certificateLocation\":null,\"certificateKeyLocation\":null,\"securityProtocols\":null,\"enabled\":true},{\"protocol\":\"https\",\"port\":\"443\",\"ipAddress\":\"*\",\"certificateLocation\":\"/etc/ssl/nginxsample/certificate.crt\",\"certificateKeyLocation\":\"/etc/ssl/nginxsample/certificate.key\",\"securityProtocols\":null,\"enabled\":true}]";
        readonly string httpAndHttpsBindingWithCertificateVariable = "[{\"protocol\":\"http\",\"port\":\"80\",\"ipAddress\":\"*\",\"certificateLocation\":null,\"certificateKeyLocation\":null,\"securityProtocols\":null,\"enabled\":true},{\"protocol\":\"https\",\"port\":\"443\",\"ipAddress\":\"*\",\"certificateLocation\":null,\"certificateKeyLocation\":null,\"securityProtocols\":null,\"enabled\":true,\"thumbprint\":null,\"certificateVariable\":\"NginxSampleWebAppCertificate\"}]";

        readonly string staticContentAndReverseProxyLocations = "[{\"path\":\"/\",\"directives\":\"{\\\"root\\\":\\\"#{Octopus.Action.Package.InstallationDirectoryPath}/wwwroot\\\",\\\"try_files\\\":\\\"$uri $uri/ /index.html\\\"}\",\"headers\":\"\",\"reverseProxy\":false,\"reverseProxyUrl\":\"\",\"reverseProxyHeaders\":\"\",\"reverseProxyDirectives\":\"\"},{\"path\":\"/api/\",\"directives\":\"\",\"headers\":\"\",\"reverseProxy\":\"True\",\"reverseProxyUrl\":\"http://localhost:5000\",\"reverseProxyHeaders\":\"{\\\"Upgrade\\\":\\\"$http_upgrade\\\",\\\"Connection\\\":\\\"keep-alive\\\",\\\"Host\\\":\\\"$host\\\",\\\"X-Forwarded-For\\\":\\\"$proxy_add_x_forwarded_for\\\",\\\"X-Forwarded-Proto\\\":\\\"$scheme\\\"}\",\"reverseProxyDirectives\":\"{\\\"proxy_http_version\\\":\\\"1.1\\\",\\\"proxy_cache_bypass\\\":\\\"$http_upgrade\\\"}\"}]";

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
                    staticContentAndReverseProxyLocations
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
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
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

        [Test]
        public void ExecuteWorks()
        {
            var packageId = "NginxSampleWebApp";
            var confDirectory = "conf";
            
            var deployment = new RunningDeployment($"C:\\{packageId}.zip", new CalamariVariableDictionary());
            deployment.Variables.Set(SpecialVariables.Package.NuGetPackageId, packageId);
            deployment.Variables.Set(SpecialVariables.Package.Output.InstallationDirectoryPath, $"/var/www/{packageId}");
            deployment.Variables.Set(SpecialVariables.Action.Nginx.Server.Bindings, httpOnlyBinding);
            deployment.Variables.Set(SpecialVariables.Action.Nginx.Server.Locations, staticContentAndReverseProxyLocations);
            deployment.Variables.Set(SpecialVariables.Action.Nginx.Server.HostName, "www.nginxsampleweb.app");

            new NginxFeature(nginxServer).Execute(deployment);

            var nginxTempDirectory = deployment.Variables.Get("OctopusNginxFeatureTempDirectory");
            Assert.That(nginxTempDirectory, Is.Not.Empty);
            
            var tempConfPath = Path.Combine(nginxTempDirectory, confDirectory);
            Assert.IsTrue(Directory.Exists(tempConfPath));
            
            var rootConf = Path.Combine(tempConfPath, $"{packageId}.conf");
            Assert.IsTrue(File.Exists(rootConf));
            
            var apiConf = Path.Combine(tempConfPath, $"{packageId}.conf.d", $"location.api.conf");
            Assert.IsTrue(File.Exists(apiConf));
            
            this.Assent(File.ReadAllText(rootConf), TestEnvironment.AssentConfiguration, $"{nameof(ExecuteWorks)}.rootLocation");
            this.Assent(File.ReadAllText(apiConf), TestEnvironment.AssentConfiguration, $"{nameof(ExecuteWorks)}.apiLocation");
        }
    }
}