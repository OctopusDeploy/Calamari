using System;
using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Certificates.Java;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Calamari.Tests.Java.Fixtures
{
    [TestFixture]
    public class JavaKeystoreBuilderFixture
    {
        InMemoryLog log;
        string workingDirectory;

        [SetUp]
        public void SetUp()
        {
            log = new InMemoryLog();
            workingDirectory = Path.Combine(Path.GetTempPath(), "JavaKeystoreBuilderFixture-" + Guid.NewGuid());
            Directory.CreateDirectory(workingDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(workingDirectory))
                Directory.Delete(workingDirectory, true);
        }

        [Test]
        public void SaveKeystoreToFile_WithRsaKeyAndSingleCertificate_ProducesAValidPkcs12KeystoreContainingTheSameKeyAndCertificate()
        {
            var (privateKeyPem, certificatePem, keyPair, certificate) = CreateSelfSignedRsaCertificate("CN=octopus-test");

            var keystorePath = Path.Combine(workingDirectory, "test.p12");
            var builder = new JavaKeystoreBuilder(log);

            var resultPath = builder.SaveKeystoreToFile("myalias", privateKeyPem, certificatePem, "sekret", keystorePath);

            resultPath.Should().Be(Path.GetFullPath(keystorePath));
            File.Exists(keystorePath).Should().BeTrue();

            var reloaded = new Pkcs12StoreBuilder().Build();
            using (var fileStream = new FileStream(keystorePath, FileMode.Open))
            {
                reloaded.Load(fileStream, "sekret".ToCharArray());
            }

            reloaded.ContainsAlias("myalias").Should().BeTrue();
            reloaded.IsKeyEntry("myalias").Should().BeTrue();

            var reloadedChain = reloaded.GetCertificateChain("myalias");
            reloadedChain.Should().HaveCount(1);
            reloadedChain[0].Certificate.CertificateStructure.Should().Be(certificate.CertificateStructure);

            var reloadedKey = reloaded.GetKey("myalias").Key;
            reloadedKey.Should().Be(keyPair.Private);
        }

        [Test]
        public void SaveKeystoreToFile_WithBlankAliasAndPassword_FallsBackToOctopusDefaults()
        {
            var (privateKeyPem, certificatePem, _, _) = CreateSelfSignedRsaCertificate("CN=octopus-defaults-test");

            var keystorePath = Path.Combine(workingDirectory, "defaults.p12");
            var builder = new JavaKeystoreBuilder(log);

            builder.SaveKeystoreToFile("", privateKeyPem, certificatePem, "", keystorePath);

            var reloaded = new Pkcs12StoreBuilder().Build();
            using (var fileStream = new FileStream(keystorePath, FileMode.Open))
            {
                reloaded.Load(fileStream, JavaKeystoreBuilder.DefaultPassword.ToCharArray());
            }

            reloaded.ContainsAlias(JavaKeystoreBuilder.DefaultAlias).Should().BeTrue();
        }

        [Test]
        public void SaveKeystoreToFile_WithRelativeKeystorePath_ThrowsCommandException()
        {
            var (privateKeyPem, certificatePem, _, _) = CreateSelfSignedRsaCertificate("CN=octopus-relative-path-test");
            var builder = new JavaKeystoreBuilder(log);

            Action act = () => builder.SaveKeystoreToFile("myalias", privateKeyPem, certificatePem, "sekret", "relative/path.p12");

            act.Should().Throw<CommandException>().WithMessage("*absolute path*");
        }

        [Test]
        public void SaveKeystoreToFile_WithNoCertificatesInPem_ThrowsCommandException()
        {
            var (privateKeyPem, _, _, _) = CreateSelfSignedRsaCertificate("CN=octopus-no-cert-test");
            var builder = new JavaKeystoreBuilder(log);
            var keystorePath = Path.Combine(workingDirectory, "no-cert.p12");

            Action act = () => builder.SaveKeystoreToFile("myalias", privateKeyPem, "not a certificate", "sekret", keystorePath);

            act.Should().Throw<CommandException>().WithMessage("*does not contain any certificates*");
        }

        [Test]
        public void BuildPkcs12Store_WithCertificateChain_IncludesEveryCertificateInOrder()
        {
            // A genuine chain, not two unrelated self-signed certificates: the intermediate is its
            // own CA, and the leaf is actually issued by (signed by) that intermediate.
            var (_, intermediateCertPem, intermediateKeyPair, intermediateCert) = CreateSelfSignedRsaCertificate("CN=intermediate");
            var (leafKeyPem, leafCertPem, _, leafCert) = CreateRsaCertificate("CN=leaf", "CN=intermediate", intermediateKeyPair);

            var builder = new JavaKeystoreBuilder(log);
            var store = builder.BuildPkcs12Store("myalias", leafKeyPem, leafCertPem + "\n" + intermediateCertPem, "sekret");

            var chain = store.GetCertificateChain("myalias");
            chain.Should().HaveCount(2);
            chain[0].Certificate.CertificateStructure.Should().Be(leafCert.CertificateStructure);
            chain[1].Certificate.CertificateStructure.Should().Be(intermediateCert.CertificateStructure);
        }

        static (string PrivateKeyPem, string CertificatePem, AsymmetricCipherKeyPair KeyPair, X509Certificate Certificate) CreateSelfSignedRsaCertificate(string subjectDn)
        {
            var keyGenerator = new RsaKeyPairGenerator();
            keyGenerator.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var keyPair = keyGenerator.GenerateKeyPair();
            return CreateRsaCertificate(subjectDn, subjectDn, keyPair, keyPair);
        }

        static (string PrivateKeyPem, string CertificatePem, AsymmetricCipherKeyPair KeyPair, X509Certificate Certificate) CreateRsaCertificate(string subjectDn, string issuerDn, AsymmetricCipherKeyPair issuerKeyPair)
        {
            var keyGenerator = new RsaKeyPairGenerator();
            keyGenerator.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var keyPair = keyGenerator.GenerateKeyPair();
            return CreateRsaCertificate(subjectDn, issuerDn, keyPair, issuerKeyPair);
        }

        static (string PrivateKeyPem, string CertificatePem, AsymmetricCipherKeyPair KeyPair, X509Certificate Certificate) CreateRsaCertificate(string subjectDn, string issuerDn, AsymmetricCipherKeyPair keyPair, AsymmetricCipherKeyPair issuerKeyPair)
        {
            var signingKeyPair = issuerKeyPair;

            var generator = new X509V3CertificateGenerator();
            generator.SetSerialNumber(Org.BouncyCastle.Math.BigInteger.ValueOf(DateTime.UtcNow.Ticks));
            generator.SetIssuerDN(new Org.BouncyCastle.Asn1.X509.X509Name(issuerDn));
            generator.SetSubjectDN(new Org.BouncyCastle.Asn1.X509.X509Name(subjectDn));
            generator.SetNotBefore(DateTime.UtcNow.AddDays(-1));
            generator.SetNotAfter(DateTime.UtcNow.AddYears(1));
            generator.SetPublicKey(keyPair.Public);

            var signatureFactory = new Org.BouncyCastle.Crypto.Operators.Asn1SignatureFactory("SHA256WITHRSA", signingKeyPair.Private);
            var certificate = generator.Generate(signatureFactory);

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

            return (privateKeyPem, certificatePem, keyPair, certificate);
        }
    }
}
