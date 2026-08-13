using System;
using System.IO;
using System.IO.Compression;
using System.Xml;
using Calamari.Common.Commands;
using Calamari.Integration.Certificates.Java.Tomcat;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace Calamari.Tests.Java.Fixtures
{
    [TestFixture]
    public class TomcatConnectorConfiguratorFixture
    {
        InMemoryLog log;
        string catalinaBase;
        string serverXmlPath;
        string privateKeyPem;
        string certificatePem;

        [SetUp]
        public void SetUp()
        {
            log = new InMemoryLog();
            catalinaBase = Path.Combine(Path.GetTempPath(), "TomcatConnectorConfiguratorFixture-" + Guid.NewGuid());
            Directory.CreateDirectory(Path.Combine(catalinaBase, "conf"));
            serverXmlPath = Path.Combine(catalinaBase, "conf", "server.xml");

            (privateKeyPem, certificatePem) = CreateSelfSignedRsaCertificate();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(catalinaBase))
                Directory.Delete(catalinaBase, true);
        }

        void WriteServerXml(string serviceXml)
        {
            File.WriteAllText(serverXmlPath, $"<Server><Service name=\"Catalina\">{serviceXml}</Service></Server>");
        }

        TomcatCertificateOptions BuildOptions(TomcatHttpsImplementation implementation, TomcatVersion version, bool isDefault = true, string hostName = "")
        {
            return new TomcatCertificateOptions(
                                                 catalinaBase,
                                                 "Catalina",
                                                 "8443",
                                                 implementation,
                                                 version,
                                                 isDefault,
                                                 hostName,
                                                 privateKeyPem,
                                                 certificatePem,
                                                 "sekret",
                                                 "myalias",
                                                 "",
                                                 "",
                                                 "");
        }

        [Test]
        public void ConfigureHttps_OnTomcat9WithNio_CreatesSslHostConfigWithKeystoreCertificate()
        {
            WriteServerXml("<Connector port=\"8443\" />");
            var options = BuildOptions(TomcatHttpsImplementation.Nio, new TomcatVersion(9, 0));

            new TomcatConnectorConfigurator(log).ConfigureHttps(options);

            var document = new XmlDocument();
            document.Load(serverXmlPath);
            var connector = (XmlElement)document.SelectSingleNode("//Connector[@port='8443']")!;

            connector.GetAttribute("protocol").Should().Be("org.apache.coyote.http11.Http11NioProtocol");
            connector.GetAttribute("SSLEnabled").Should().Be("true");

            var certificate = (XmlElement)connector.SelectSingleNode("SSLHostConfig/Certificate")!;
            certificate.GetAttribute("type").Should().Be("RSA");
            certificate.GetAttribute("certificateKeystorePassword").Should().Be("sekret");
            certificate.GetAttribute("certificateKeyAlias").Should().Be("myalias");
            certificate.GetAttribute("certificateKeystoreFile").Should().StartWith("${catalina.base}");

            var keystorePath = certificate.GetAttribute("certificateKeystoreFile").Replace("${catalina.base}", catalinaBase);
            File.Exists(keystorePath).Should().BeTrue();
        }

        [Test]
        public void ConfigureHttps_OnTomcat7WithNio_SetsAttributesDirectlyOnConnector()
        {
            WriteServerXml("<Connector port=\"8443\" />");
            var options = BuildOptions(TomcatHttpsImplementation.Nio, new TomcatVersion(7, 0));

            new TomcatConnectorConfigurator(log).ConfigureHttps(options);

            var document = new XmlDocument();
            document.Load(serverXmlPath);
            var connector = (XmlElement)document.SelectSingleNode("//Connector[@port='8443']")!;

            connector.GetAttribute("protocol").Should().Be("org.apache.coyote.http11.Http11NioProtocol");
            connector.GetAttribute("keystorePass").Should().Be("sekret");
            connector.GetAttribute("keyAlias").Should().Be("myalias");
            connector.SelectSingleNode("SSLHostConfig").Should().BeNull("Tomcat 7 has no SSLHostConfig concept");
        }

        [Test]
        public void ConfigureHttps_WithApr_WritesRawPemFilesInsteadOfAKeystore()
        {
            WriteServerXml("<Connector port=\"8443\" />");
            var options = BuildOptions(TomcatHttpsImplementation.Apr, new TomcatVersion(9, 0));

            new TomcatConnectorConfigurator(log).ConfigureHttps(options);

            var document = new XmlDocument();
            document.Load(serverXmlPath);
            var connector = (XmlElement)document.SelectSingleNode("//Connector[@port='8443']")!;
            var certificate = (XmlElement)connector.SelectSingleNode("SSLHostConfig/Certificate")!;

            certificate.HasAttribute("certificateKeystoreFile").Should().BeFalse();
            certificate.GetAttribute("certificateKeyFile").Should().StartWith("${catalina.base}");
            certificate.GetAttribute("certificateFile").Should().StartWith("${catalina.base}");

            var keyPath = certificate.GetAttribute("certificateKeyFile").Replace("${catalina.base}", catalinaBase);
            File.ReadAllText(keyPath).Should().Be(privateKeyPem);
        }

        [Test]
        public void ConfigureHttps_WithNonDefaultHostname_SetsHostNameOnSslHostConfig()
        {
            WriteServerXml("<Connector port=\"8443\" />");
            // Configure the connector's default certificate first (establishing
            // defaultSSLHostConfigName), then add a second, SNI-specific certificate for a different
            // hostname - the second call must not disturb the first one's default host marker.
            var defaultOptions = BuildOptions(TomcatHttpsImplementation.Nio, new TomcatVersion(9, 0), isDefault: true, hostName: "");
            new TomcatConnectorConfigurator(log).ConfigureHttps(defaultOptions);

            var sniOptions = BuildOptions(TomcatHttpsImplementation.Nio, new TomcatVersion(9, 0), isDefault: false, hostName: "www.example.com");
            new TomcatConnectorConfigurator(log).ConfigureHttps(sniOptions);

            var document = new XmlDocument();
            document.Load(serverXmlPath);
            var connector = (XmlElement)document.SelectSingleNode("//Connector[@port='8443']")!;

            connector.HasAttribute(TomcatConnectorAttributes.DefaultSslHostConfigName).Should().BeFalse("the default host was never renamed away from _default_, so no explicit marker is needed");
            var sniHostConfig = (XmlElement)connector.SelectSingleNode("SSLHostConfig[@hostName='www.example.com']")!;
            sniHostConfig.GetAttribute("hostName").Should().Be("www.example.com");
            connector.SelectNodes("SSLHostConfig")!.Count.Should().Be(2, "the default host's SSLHostConfig and the SNI one should coexist");
        }

        [Test]
        public void ConfigureHttps_WhenServiceIsMissing_ThrowsCommandException()
        {
            File.WriteAllText(serverXmlPath, "<Server></Server>");
            var options = BuildOptions(TomcatHttpsImplementation.Nio, new TomcatVersion(9, 0));

            Action act = () => new TomcatConnectorConfigurator(log).ConfigureHttps(options);

            act.Should().Throw<CommandException>().WithMessage("*Catalina*");
        }

        [Test]
        public void ConfigureHttps_WhenConnectorDoesNotExist_CreatesOne()
        {
            WriteServerXml("");
            var options = BuildOptions(TomcatHttpsImplementation.Nio, new TomcatVersion(9, 0));

            new TomcatConnectorConfigurator(log).ConfigureHttps(options);

            var document = new XmlDocument();
            document.Load(serverXmlPath);
            document.SelectSingleNode("//Connector[@port='8443']").Should().NotBeNull();
        }

        [Test]
        public void ConfigureHttps_WithNio2OnTomcat7_ThrowsCommandException()
        {
            WriteServerXml("<Connector port=\"8443\" />");
            var options = BuildOptions(TomcatHttpsImplementation.Nio2, new TomcatVersion(7, 0));

            Action act = () => new TomcatConnectorConfigurator(log).ConfigureHttps(options);

            act.Should().Throw<CommandException>().WithMessage("*Non-Blocking IO 2*");
        }

        [Test]
        public void ConfigureHttps_WithBioOnTomcat9_ThrowsCommandException()
        {
            WriteServerXml("<Connector port=\"8443\" />");
            var options = BuildOptions(TomcatHttpsImplementation.Bio, new TomcatVersion(9, 0));

            Action act = () => new TomcatConnectorConfigurator(log).ConfigureHttps(options);

            act.Should().Throw<CommandException>().WithMessage("*Blocking IO*");
        }

        [Test]
        public void ConfigureHttps_BacksUpTheOriginalServerXmlIntoAZip()
        {
            WriteServerXml("<Connector port=\"8443\" />");
            var originalContent = File.ReadAllText(serverXmlPath);
            var options = BuildOptions(TomcatHttpsImplementation.Nio, new TomcatVersion(9, 0));

            new TomcatConnectorConfigurator(log).ConfigureHttps(options);

            var backupZipPath = Path.Combine(catalinaBase, "conf", "octopus_backup.zip");
            File.Exists(backupZipPath).Should().BeTrue();

            using (var archive = ZipFile.OpenRead(backupZipPath))
            {
                archive.Entries.Should().HaveCount(1);
                using (var reader = new StreamReader(archive.Entries[0].Open()))
                {
                    reader.ReadToEnd().Should().Be(originalContent);
                }
            }
        }

        static (string PrivateKeyPem, string CertificatePem) CreateSelfSignedRsaCertificate()
        {
            var keyGenerator = new RsaKeyPairGenerator();
            keyGenerator.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var keyPair = keyGenerator.GenerateKeyPair();

            var generator = new Org.BouncyCastle.X509.X509V3CertificateGenerator();
            var subject = new Org.BouncyCastle.Asn1.X509.X509Name("CN=octopus-tomcat-test");
            generator.SetSerialNumber(Org.BouncyCastle.Math.BigInteger.ValueOf(DateTime.UtcNow.Ticks));
            generator.SetIssuerDN(subject);
            generator.SetSubjectDN(subject);
            generator.SetNotBefore(DateTime.UtcNow.AddDays(-1));
            generator.SetNotAfter(DateTime.UtcNow.AddYears(1));
            generator.SetPublicKey(keyPair.Public);
            var certificate = generator.Generate(new Org.BouncyCastle.Crypto.Operators.Asn1SignatureFactory("SHA256WITHRSA", keyPair.Private));

            string privateKeyPem;
            using (var writer = new StringWriter())
            {
                new PemWriter(writer).WriteObject(keyPair.Private);
                privateKeyPem = writer.ToString();
            }

            string certificatePem;
            using (var writer = new StringWriter())
            {
                new PemWriter(writer).WriteObject(certificate);
                certificatePem = writer.ToString();
            }

            return (privateKeyPem, certificatePem);
        }
    }
}
