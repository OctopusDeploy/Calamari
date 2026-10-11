using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Polly;
using Polly.Retry;

namespace Calamari.Integration.Certificates.Java
{
    /// <summary>
    /// Builds a PKCS#12 keystore from a PEM-encoded private key and certificate chain, using
    /// BouncyCastle.Cryptography, without requiring a JVM or the Octopus.Dependencies.Java tool
    /// package.
    ///
    /// This replicates the behaviour of the "Deploy a Keystore to the Filesystem" step's existing
    /// implementation (com.octopus.calamari.keystore.KeystoreConfig, in the Octopus.Dependencies.Java
    /// repository), with one deliberate difference: that implementation builds a JKS keystore via the
    /// JDK's own built-in security provider (BouncyCastle there is only used to parse the incoming PEM
    /// material) - JKS has no equivalent writer in .NET, so this produces a PKCS#12 keystore instead.
    /// PKCS#12 is accepted anywhere JKS currently is, provided the consuming Tomcat/WildFly
    /// configuration is told the keystore type explicitly (both currently rely on an unstated JKS
    /// default).
    /// </summary>
    public class JavaKeystoreBuilder
    {
        public const string DefaultAlias = "octopus";
        public const string DefaultPassword = "changeit";

        static readonly Regex BeginPrivateKey = new Regex(@"-+BEGIN\s+.*PRIVATE\s+KEY", RegexOptions.IgnoreCase);

        readonly ILog log;

        public JavaKeystoreBuilder(ILog log)
        {
            this.log = log;
        }

        /// <summary>
        /// Builds a PKCS#12 store in memory containing the given private key and certificate chain
        /// under the given alias, protected by the given password. The key entry's password and the
        /// store's own password are always the same value - Tomcat, in particular, does not support a
        /// mismatch between the two.
        /// </summary>
        public Pkcs12Store BuildPkcs12Store(string alias, string privateKeyPem, string certificateChainPem, string password)
        {
            var fixedAlias = string.IsNullOrWhiteSpace(alias) ? DefaultAlias : alias;
            var fixedPassword = string.IsNullOrWhiteSpace(password) ? DefaultPassword : password;

            var privateKey = ParsePrivateKey(privateKeyPem);
            var certificateChain = ParseCertificateChain(certificateChainPem);

            var store = new Pkcs12StoreBuilder().Build();
            var certificateEntries = certificateChain.Select(c => new X509CertificateEntry(c)).ToArray();
            store.SetKeyEntry(fixedAlias, new AsymmetricKeyEntry(privateKey), certificateEntries);

            return store;
        }

        /// <summary>
        /// Builds a PKCS#12 store (see <see cref="BuildPkcs12Store"/>) and writes it to disk, retrying
        /// on failure with an exponential backoff (matching the retry policy used by the existing
        /// Java-based implementation: 5 attempts total, starting at 5 seconds and doubling).
        /// </summary>
        /// <returns>The absolute path the keystore was written to.</returns>
        public string SaveKeystoreToFile(string alias, string privateKeyPem, string certificateChainPem, string password, string keystoreFilename)
        {
            if (string.IsNullOrWhiteSpace(keystoreFilename))
                throw new CommandException("The keystore filename must be supplied.");

            if (!Path.IsPathRooted(keystoreFilename))
                throw new CommandException("The keystore filename must be an absolute path.");

            var fixedPassword = string.IsNullOrWhiteSpace(password) ? DefaultPassword : password;

            // Parsing the key/certificate is deterministic - a malformed PEM will fail exactly the
            // same way on every attempt, so it's built once, outside the retry pipeline below. Only
            // the file write itself (which can hit transient permission/disk contention) is retried.
            var store = BuildPkcs12Store(alias, privateKeyPem, certificateChainPem, fixedPassword);

            CreateRetryPipeline().Execute(() =>
            {
                using (var fileStream = new FileStream(keystoreFilename, FileMode.Create))
                {
                    store.Save(fileStream, fixedPassword.ToCharArray(), new SecureRandom());
                }

                // The save operation may not fail even if something prevented the file from actually
                // being created, so check for its existence explicitly.
                if (!File.Exists(keystoreFilename))
                    throw new CommandException($"File was not created at {keystoreFilename}");

                log.Verbose($"Successfully created keystore at {keystoreFilename}");
            });

            return Path.GetFullPath(keystoreFilename);
        }

        ResiliencePipeline CreateRetryPipeline()
        {
            return new ResiliencePipelineBuilder()
                   .AddRetry(new RetryStrategyOptions
                   {
                       ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                       MaxRetryAttempts = 4,
                       Delay = TimeSpan.FromSeconds(5),
                       BackoffType = DelayBackoffType.Exponential,
                       OnRetry = args =>
                                 {
                                     log.Verbose($"Failed to create the keystore file, waiting {args.RetryDelay.TotalSeconds}s before trying again. {args.Outcome.Exception?.Message}");
                                     return default;
                                 }
                   })
                   .Build();
        }

        /// <summary>
        /// Returns "RSA", "EC" or "DSA" for the given PEM-encoded private key - used by callers (such
        /// as the Tomcat certificate configurator) that need to know the key's algorithm to write it
        /// into a config file correctly, without exposing BouncyCastle's own key-parameter types.
        /// </summary>
        public static string GetPrivateKeyAlgorithm(string privateKeyPem)
        {
            var key = ParsePrivateKey(privateKeyPem);
            switch (key)
            {
                case ECPrivateKeyParameters _:
                    return "EC";
                case DsaPrivateKeyParameters _:
                    return "DSA";
                case RsaKeyParameters _:
                    return "RSA";
                default:
                    throw new CommandException($"Unrecognised private key algorithm: {key.GetType().Name}.");
            }
        }

        static AsymmetricKeyParameter ParsePrivateKey(string pem)
        {
            var match = BeginPrivateKey.Match(pem);
            if (!match.Success)
                throw new CommandException("The private key does not contain a recognisable PEM private key block.");

            var trimmed = pem.Substring(match.Index);

            object pemObject;
            try
            {
                using (var reader = new StringReader(trimmed))
                {
                    pemObject = new PemReader(reader).ReadObject();
                }
            }
            catch (Exception ex)
            {
                // BouncyCastle's PemReader expects an EC private key to carry its public point
                // alongside it. Octopus sometimes supplies EC private keys in isolation, so fall back
                // to manually parsing the SEC1 structure when the standard parser can't handle it.
                var fallback = TryParseEcPrivateKeyWithoutPublicPoint(trimmed);
                if (fallback == null)
                    throw new CommandException("Unable to parse the private key.", ex);

                return fallback;
            }

            switch (pemObject)
            {
                case AsymmetricCipherKeyPair keyPair:
                    return keyPair.Private;
                case AsymmetricKeyParameter keyParameter:
                    return keyParameter;
                default:
                    throw new CommandException($"Unrecognised private key format: {pemObject?.GetType().Name ?? "null"}.");
            }
        }

        static AsymmetricKeyParameter? TryParseEcPrivateKeyWithoutPublicPoint(string pem)
        {
            if (!pem.Contains("EC PRIVATE KEY"))
                return null;

            try
            {
                using (var reader = new StringReader(pem))
                {
                    var pemObject = new Org.BouncyCastle.Utilities.IO.Pem.PemReader(reader).ReadPemObject();
                    var ecPrivateKeyStructure = ECPrivateKeyStructure.GetInstance(pemObject.Content);

                    var parametersObject = ecPrivateKeyStructure.GetParameters();
                    var curveParameters = parametersObject is Org.BouncyCastle.Asn1.DerObjectIdentifier namedCurveOid
                        ? Org.BouncyCastle.Asn1.X9.ECNamedCurveTable.GetByOid(namedCurveOid)
                        : Org.BouncyCastle.Asn1.X9.X9ECParameters.GetInstance(parametersObject);

                    return new ECPrivateKeyParameters(
                                                       ecPrivateKeyStructure.GetKey(),
                                                       new Org.BouncyCastle.Crypto.Parameters.ECDomainParameters(curveParameters.Curve, curveParameters.G, curveParameters.N, curveParameters.H, curveParameters.GetSeed()));
                }
            }
            catch
            {
                return null;
            }
        }

        static IList<X509Certificate> ParseCertificateChain(string pem)
        {
            var certificates = new List<X509Certificate>();

            // The certificate PEM may contain a chain (leaf followed by one or more intermediates)
            // concatenated together. X509CertificateParser.ReadCertificate can be called repeatedly
            // against the same stream to read each one in turn until the stream is exhausted.
            using (var stream = new MemoryStream(Encoding.ASCII.GetBytes(pem)))
            {
                var parser = new X509CertificateParser();
                while (true)
                {
                    var certificate = parser.ReadCertificate(stream);
                    if (certificate == null)
                        break;

                    certificates.Add(certificate);
                }
            }

            if (!certificates.Any())
                throw new CommandException("Certificate file does not contain any certificates. This is probably because the input certificate file is invalid.");

            return certificates;
        }
    }
}
