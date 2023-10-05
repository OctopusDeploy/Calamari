using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Assent;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Features;
using Calamari.Integration.Nginx;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;

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

        readonly string staticContentAndReverseProxyLocations = "[{\"path\":\"= /\",\"directives\":\"[{\\\"root\\\":\\\"#{Octopus.Action.Package.InstallationDirectoryPath}/wwwroot\\\"},{\\\"try_files\\\":\\\"$uri $uri/ /index.html\\\"}]\",\"headers\":\"\",\"reverseProxy\":false,\"reverseProxyUrl\":\"\",\"reverseProxyHeaders\":\"\",\"reverseProxyDirectives\":\"\"},{\"path\":\"/\",\"directives\":\"[{\\\"root\\\":\\\"#{Octopus.Action.Package.InstallationDirectoryPath}/wwwroot\\\"},{\\\"try_files\\\":\\\"$uri $uri/ /index.html\\\"}]\",\"headers\":\"\",\"reverseProxy\":false,\"reverseProxyUrl\":\"\",\"reverseProxyHeaders\":\"\",\"reverseProxyDirectives\":\"\"},{\"path\":\"/api/\",\"directives\":\"\",\"headers\":\"\",\"reverseProxy\":\"True\",\"reverseProxyUrl\":\"http://localhost:5000\",\"reverseProxyHeaders\":\"{\\\"Upgrade\\\":\\\"$http_upgrade\\\",\\\"Connection\\\":\\\"keep-alive\\\",\\\"Host\\\":\\\"$host\\\",\\\"X-Forwarded-For\\\":\\\"$proxy_add_x_forwarded_for\\\",\\\"X-Forwarded-Proto\\\":\\\"$scheme\\\"}\",\"reverseProxyDirectives\":\"{\\\"proxy_http_version\\\":\\\"1.1\\\",\\\"proxy_cache_bypass\\\":\\\"$http_upgrade\\\"}\"}]";

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
                    "[{\"path\":\"/\",\"directives\":\"[{\\\"root\\\":\\\"#{Octopus.Action.Package.InstallationDirectoryPath}/wwwroot\\\"},{\\\"try_files\\\":\\\"$uri $uri/ /index.html\\\"}]\",\"headers\":\"\",\"reverseProxy\":false,\"reverseProxyUrl\":\"\",\"reverseProxyHeaders\":\"\",\"reverseProxyDirectives\":\"\"}]");
            
            var virtualServerName = "StaticContent";

            nginxServer
                .WithVirtualServerName(virtualServerName)
                .WithServerBindings(JsonConvert.DeserializeObject<IEnumerable<Binding>>(httpOnlyBinding),
                    new Dictionary<string, (string, string, string, string)>())
                .WithRootLocation(locations.First());

            nginxServer.BuildConfiguration();
            nginxServer.SaveConfiguration(tempDirectory);
            
            var nginxConfigFilePath = Path.Combine(tempDirectory, "conf", $"{virtualServerName}.conf");
            this.Assent(File.ReadAllText(nginxConfigFilePath), AssentConfiguration.Default);
        }

        [Test]
        public void SetupStaticContentSiteWithMultipleIncludeDirectives()
        {
            var locations =
                JsonConvert.DeserializeObject<IEnumerable<Location>>(
                    "[{\"path\":\"/\",\"directives\":\"[{\\\"root\\\":\\\"#{Octopus.Action.Package.InstallationDirectoryPath}/wwwroot\\\"},{\\\"try_files\\\":\\\"$uri $uri/ /index.html\\\"},{\\\"include\\\":\\\"fastcgi_params\\\"},{\\\"include\\\":\\\"/etc/nginx/api_proxy.conf\\\"}]\",\"headers\":\"\",\"reverseProxy\":false,\"reverseProxyUrl\":\"\",\"reverseProxyHeaders\":\"\",\"reverseProxyDirectives\":\"\"}]");

            var virtualServerName = "StaticContent";

            nginxServer
                .WithVirtualServerName(virtualServerName)
                .WithServerBindings(JsonConvert.DeserializeObject<IEnumerable<Binding>>(httpOnlyBinding),
                    new Dictionary<string, (string, string, string, string)>())
                .WithRootLocation(locations.First());

            nginxServer.BuildConfiguration();
            nginxServer.SaveConfiguration(tempDirectory);
            
            var nginxConfigFilePath = Path.Combine(tempDirectory, "conf", $"{virtualServerName}.conf");
            this.Assent(File.ReadAllText(nginxConfigFilePath), AssentConfiguration.Default);
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
                    new Dictionary<string, (string, string, string, string)>())
                .WithRootLocation(locations.First());

            nginxServer.BuildConfiguration();
            nginxServer.SaveConfiguration(tempDirectory);
            
            var nginxConfigFilePath = Path.Combine(tempDirectory, "conf", $"{virtualServerName}.conf");
            this.Assent(File.ReadAllText(nginxConfigFilePath), AssentConfiguration.Default);
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
                    new Dictionary<string, (string, string, string, string)>())
                .WithRootLocation(rootLocation)
                .WithAdditionalLocations(new []{apiLocation});

            nginxServer.BuildConfiguration();
            nginxServer.SaveConfiguration(tempDirectory);
            
            var rootConfigFilePath = Path.Combine(tempDirectory, "conf", $"{virtualServerName}.conf");
            this.Assent(
                File.ReadAllText(rootConfigFilePath), 
                AssentConfiguration.Default,
                $"{nameof(SetupStaticContentWithReverseProxySite)}.rootLocation"
            );

            var apiConfigFilePath = Path.Combine(
                tempDirectory, 
                "conf", 
                $"{virtualServerName}.conf.d",
                $"location.0{apiLocation.Path.Trim('/')}.conf"
            );
            
            this.Assent(
                File.ReadAllText(apiConfigFilePath), 
                AssentConfiguration.Default,
                $"{nameof(SetupStaticContentWithReverseProxySite)}.apiLocation"
            );
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNix)]
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
                    new Dictionary<string, (string, string, string, string)>())
                .WithRootLocation(locations.First());

            nginxServer.BuildConfiguration();
            nginxServer.SaveConfiguration(tempDirectory);
            
            var nginxConfigFilePath = Path.Combine(tempDirectory, "conf", $"{virtualServerName}.conf");
            this.Assent(File.ReadAllText(nginxConfigFilePath), AssentConfiguration.Default);
        }

        [Test]
        public void SetupReverseProxyWithSslUsingCertificateVariableSite()
        {
            var (certificatePem, certificatePrivateKeyPem, certificateChainPem) = GetCertificateDetails();

            var locations =
                JsonConvert.DeserializeObject<IEnumerable<Location>>(
                    "[{\"path\":\"/\",\"directives\":\"\",\"headers\":\"\",\"reverseProxy\":\"True\",\"reverseProxyUrl\":\"http://localhost:5000\",\"reverseProxyHeaders\":\"{\\\"Upgrade\\\":\\\"$http_upgrade\\\",\\\"Connection\\\":\\\"keep-alive\\\",\\\"Host\\\":\\\"$host\\\",\\\"X-Forwarded-For\\\":\\\"$proxy_add_x_forwarded_for\\\",\\\"X-Forwarded-Proto\\\":\\\"$scheme\\\"}\",\"reverseProxyDirectives\":\"{\\\"proxy_http_version\\\":\\\"1.1\\\",\\\"proxy_cache_bypass\\\":\\\"$http_upgrade\\\"}\"}]"
                );

            var subjectName = "www.nginxsamplewebapp.com";
            var virtualServerName = "HttpsReverseProxy";
            var certificates = new Dictionary<string, (string, string, string, string)>{
                {"NginxSampleWebAppCertificate", (subjectName, certificatePem, certificatePrivateKeyPem, certificateChainPem)}
            };

            nginxServer
                .WithVirtualServerName(virtualServerName)
                .WithServerBindings(JsonConvert.DeserializeObject<IEnumerable<Binding>>(httpAndHttpsBindingWithCertificateVariable),
                    certificates)
                .WithRootLocation(locations.First());

            nginxServer.BuildConfiguration();
            nginxServer.SaveConfiguration(tempDirectory);
            
            var nginxConfigFilePath = Path.Combine(tempDirectory, "conf", $"{virtualServerName}.conf");
            this.Assent(File.ReadAllText(nginxConfigFilePath), AssentConfiguration.Default);
#if NETCORE
            var sslCertFilePath = Path.Combine(tempDirectory, "ssl", subjectName);
            this.Assent(File.ReadAllText(Path.Combine(sslCertFilePath, $"{subjectName}.crt")), AssentConfiguration.Default, $"{nameof(SetupReverseProxyWithSslUsingCertificateVariableSite)}.crt");
#endif
        }

        [Test]
        public void ExecuteWorks()
        {
            var packageId = "NginxSampleWebApp";
            var confDirectory = "conf";
            
            var deployment = new RunningDeployment($"C:\\{packageId}.zip", new CalamariVariables());
            deployment.Variables.Set(PackageVariables.PackageId, packageId);
            deployment.Variables.Set(PackageVariables.Output.InstallationDirectoryPath, $"/var/www/{packageId}");
            deployment.Variables.Set(SpecialVariables.Action.Nginx.Server.Bindings, httpOnlyBinding);
            deployment.Variables.Set(SpecialVariables.Action.Nginx.Server.Locations, staticContentAndReverseProxyLocations);
            deployment.Variables.Set(SpecialVariables.Action.Nginx.Server.HostName, "www.nginxsampleweb.app");

            new NginxFeature(
                nginxServer,
                CalamariPhysicalFileSystem.GetPhysicalFileSystem()
            ).Execute(deployment);

            var nginxTempDirectory = deployment.Variables.Get("OctopusNginxFeatureTempDirectory");
            Assert.That(nginxTempDirectory, Is.Not.Empty);
            
            var tempConfPath = Path.Combine(nginxTempDirectory, confDirectory);
            Assert.IsTrue(Directory.Exists(tempConfPath));
            
            var exactRootConf = Path.Combine(tempConfPath, $"{packageId}.conf");
            Assert.IsTrue(File.Exists(exactRootConf));

            var rootConf = Path.Combine(tempConfPath, $"{packageId}.conf.d", "location.0.conf");
            Assert.IsTrue(File.Exists(rootConf));

            var apiConf = Path.Combine(tempConfPath, $"{packageId}.conf.d", "location.1api.conf");
            Assert.IsTrue(File.Exists(apiConf));
            
            this.Assent(File.ReadAllText(exactRootConf), AssentConfiguration.Default, $"{nameof(ExecuteWorks)}.rootLocation");
            this.Assent(File.ReadAllText(rootConf), AssentConfiguration.Default, $"{nameof(ExecuteWorks)}.0");
            this.Assent(File.ReadAllText(apiConf), AssentConfiguration.Default, $"{nameof(ExecuteWorks)}.apiLocation");
        }
        
        [Test]
        public void TestLocationsUnsuitableForFilenames()
        {
            /*
             * Here we have two locations based on regular expressions: '= \' and '^/[a-z]something'. While these are different
             * locations as far as NGINX is concerned, we need to be sure they are correctly converted into individual conf files
             * after the locations are sanitized into strings suitable for filenames.
             */
            var additionalLocations =
                JsonConvert.DeserializeObject<IEnumerable<Location>>(
                    "[{\"path\":\"= /\",\"directives\":\"{\\\"root\\\":\\\"#{Octopus.Action.Package.InstallationDirectoryPath}/wwwroot\\\",\\\"index\\\":\\\"index.html\\\"}\",\"headers\":\"\",\"reverseProxy\":false,\"reverseProxyUrl\":\"\",\"reverseProxyHeaders\":\"\",\"reverseProxyDirectives\":\"\"}, {\"path\":\"^/[a-z]something\",\"directives\":\"{\\\"root\\\":\\\"#{Octopus.Action.Package.InstallationDirectoryPath}/wwwroot\\\",\\\"index\\\":\\\"index.html\\\"}\",\"headers\":\"\",\"reverseProxy\":false,\"reverseProxyUrl\":\"\",\"reverseProxyHeaders\":\"\",\"reverseProxyDirectives\":\"\"}]");
            var rootLocation =
                JsonConvert.DeserializeObject<Location>(
                    "{\"path\":\"/\",\"directives\":\"{\\\"root\\\":\\\"#{Octopus.Action.Package.InstallationDirectoryPath}/wwwroot\\\",\\\"try_files\\\":\\\"$uri $uri/ /index.html\\\"}\",\"headers\":\"\",\"reverseProxy\":false,\"reverseProxyUrl\":\"\",\"reverseProxyHeaders\":\"\",\"reverseProxyDirectives\":\"\"}");
            
            // Make sure slashes don't create nested directories. GitHub packages follow this package ID format.
            var virtualServerName = "StaticContent/MyTest";

            nginxServer
                .WithVirtualServerName(virtualServerName)
                .WithServerBindings(JsonConvert.DeserializeObject<IEnumerable<Binding>>(httpOnlyBinding),
                    new Dictionary<string, (string, string, string, string)>())
                .WithRootLocation(rootLocation)
                .WithAdditionalLocations(additionalLocations);

            nginxServer.BuildConfiguration();
            nginxServer.SaveConfiguration(tempDirectory);
            
            var nginxConfigFilePath = Path.Combine(tempDirectory, "conf", $"{nginxServer.VirtualServerName}.conf.d");
            
            // The two additional locations must be files in the conf.d directory 
            Assert.IsTrue(Directory.GetFiles(nginxConfigFilePath).Length == 2);
            // Ensure there is a single file and a single directory in the nginx conf directory. This ensures that there are no unexpected subdirectories.
            Assert.IsTrue(Directory.GetFiles(Path.Combine(tempDirectory, "conf")).Length == 1);
            Assert.IsTrue(Directory.GetDirectories(Path.Combine(tempDirectory, "conf")).Length == 1);
            // Ensure the filename matches the expected format.
            Assert.IsTrue(Directory.GetFiles(Path.Combine(tempDirectory, "conf"))[0].EndsWith($"{nginxServer.VirtualServerName}.conf"));
        }

        (string, string, string) GetCertificateDetails()
        {
#if NETCORE
            var chainCertFilePath = TestEnvironment.GetTestPath("Helpers", "Certificates", "SampleCertificateFiles", "3-cert-chain.pfx");
            var certificateCollection = new X509Certificate2Collection();
            certificateCollection.Import(chainCertFilePath, "hello world", X509KeyStorageFlags.PersistKeySet);

            var certificate = certificateCollection.First();
            var certificatePem = new string(PemEncoding.Write("CERTIFICATE", certificate.RawData));
            var key = (AsymmetricAlgorithm)certificate.GetRSAPrivateKey() ?? certificate.GetECDsaPrivateKey();
            var privateKeyBytes = key.ExportPkcs8PrivateKey();
            var certificatePrivateKeyPem = new string(PemEncoding.Write("PRIVATE KEY", privateKeyBytes));

            string certificateChainPem = null;
            foreach (var cert in certificateCollection.Skip(1))
            {
                // if the cert has no extension or the cert has a basic constraints extension,
                // and the certificate authority is true then this is a chain certificate
                certificateChainPem += $@"{new string(PemEncoding.Write("CERTIFICATE", cert.RawData))}
";
            }

            return (certificatePem, certificatePrivateKeyPem, certificateChainPem);
#else
            return (string.Empty, string.Empty, string.Empty);
#endif
        }
    }
}