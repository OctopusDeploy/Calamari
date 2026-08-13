using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Features.Java;
using Calamari.Deployment.Features.Java.Actions;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace Calamari.Tests.Java.Fixtures
{
    [TestFixture]
    public class JavaKeystoreActionFixture
    {
        const string CertificateVariableName = "MyCertificate";

        ICommandLineRunner commandLineRunner;
        InMemoryLog log;
        string workingDirectory;
        string privateKeyPem;
        string certificatePem;

        [SetUp]
        public void SetUp()
        {
            commandLineRunner = Substitute.For<ICommandLineRunner>();
            commandLineRunner.Execute(Arg.Any<CommandLineInvocation>()).Returns(new CommandResult("", 0));
            log = new InMemoryLog();
            workingDirectory = Path.Combine(Path.GetTempPath(), "JavaKeystoreActionFixture-" + System.Guid.NewGuid());
            Directory.CreateDirectory(workingDirectory);

            var keyGenerator = new RsaKeyPairGenerator();
            keyGenerator.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var keyPair = keyGenerator.GenerateKeyPair();

            var generator = new Org.BouncyCastle.X509.X509V3CertificateGenerator();
            var subject = new Org.BouncyCastle.Asn1.X509.X509Name("CN=octopus-action-test");
            generator.SetSerialNumber(Org.BouncyCastle.Math.BigInteger.ValueOf(System.DateTime.UtcNow.Ticks));
            generator.SetIssuerDN(subject);
            generator.SetSubjectDN(subject);
            generator.SetNotBefore(System.DateTime.UtcNow.AddDays(-1));
            generator.SetNotAfter(System.DateTime.UtcNow.AddYears(1));
            generator.SetPublicKey(keyPair.Public);
            var certificate = generator.Generate(new Org.BouncyCastle.Crypto.Operators.Asn1SignatureFactory("SHA256WITHRSA", keyPair.Private));

            using (var writer = new StringWriter())
            {
                new PemWriter(writer).WriteObject(keyPair.Private);
                privateKeyPem = writer.ToString();
            }

            using (var writer = new StringWriter())
            {
                new PemWriter(writer).WriteObject(certificate);
                certificatePem = writer.ToString();
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(workingDirectory))
                Directory.Delete(workingDirectory, true);
        }

        RunningDeployment BuildDeployment(bool nativeToggleEnabled, string keystoreFilename)
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.Action.Java.JavaKeystore.Variable, CertificateVariableName);
            variables.Set(SpecialVariables.Action.Java.JavaKeystore.Password, "sekret");
            variables.Set(SpecialVariables.Action.Java.JavaKeystore.KeystoreFilename, keystoreFilename);
            variables.Set(SpecialVariables.Action.Java.JavaKeystore.KeystoreAlias, "myalias");
            variables.Set(SpecialVariables.Certificate.PrivateKeyPem(CertificateVariableName), privateKeyPem);
            variables.Set(SpecialVariables.Certificate.CertificatePem(CertificateVariableName), certificatePem);
            variables.Set(SpecialVariables.Certificate.Subject(CertificateVariableName), "CN=octopus-action-test");

            if (nativeToggleEnabled)
                variables.Set(KnownVariables.EnabledFeatureToggles, OctopusFeatureToggles.KnownSlugs.JavaKeystoreNativeBouncyCastle);

            return new RunningDeployment(variables);
        }

        [Test]
        public void Execute_WhenNativeToggleEnabled_WritesKeystoreWithoutInvokingJava()
        {
            var keystorePath = Path.Combine(workingDirectory, "test.p12");
            var deployment = BuildDeployment(nativeToggleEnabled: true, keystoreFilename: keystorePath);
            var action = new JavaKeystoreAction(new JavaRunner(commandLineRunner, deployment.Variables), log);

            action.Execute(deployment);

            File.Exists(keystorePath).Should().BeTrue();
            commandLineRunner.DidNotReceive().Execute(Arg.Any<CommandLineInvocation>());
        }

        [Test]
        public void Execute_WhenNativeToggleDisabled_FallsBackToInvokingCalamariJar()
        {
            var keystorePath = Path.Combine(workingDirectory, "test.p12");
            var deployment = BuildDeployment(nativeToggleEnabled: false, keystoreFilename: keystorePath);
            var action = new JavaKeystoreAction(new JavaRunner(commandLineRunner, deployment.Variables), log);

            action.Execute(deployment);

            File.Exists(keystorePath).Should().BeFalse("the legacy path only writes the keystore via the mocked-out java process, which doesn't actually run");
            commandLineRunner.Received().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("com.octopus.calamari.keystore.KeystoreConfig")));
        }
    }
}
