using System.IO;
using Calamari.Common.Commands;

namespace Calamari.Integration.Certificates.Java.Tomcat
{
    /// <summary>
    /// Matches com.octopus.calamari.tomcathttps.TomcatHttpsOptions (Octopus.Dependencies.Java).
    /// </summary>
    public class TomcatCertificateOptions
    {
        public const string DefaultHostName = "_default_";

        public string CatalinaBase { get; }
        public string Service { get; }
        public string Port { get; }
        public TomcatHttpsImplementation Implementation { get; }
        public TomcatVersion Version { get; }
        public bool IsDefault { get; }
        public string HostName { get; }

        public string PrivateKeyPem { get; }
        public string CertificatePem { get; }
        public string KeystorePassword { get; }
        public string KeystoreAlias { get; }
        public string KeystoreFilename { get; }
        public string PrivateKeyFilename { get; }
        public string PublicKeyFilename { get; }

        public TomcatCertificateOptions(
            string catalinaBase,
            string service,
            string port,
            TomcatHttpsImplementation implementation,
            TomcatVersion version,
            bool isDefault,
            string hostName,
            string privateKeyPem,
            string certificatePem,
            string keystorePassword,
            string keystoreAlias,
            string keystoreFilename,
            string privateKeyFilename,
            string publicKeyFilename)
        {
            CatalinaBase = catalinaBase;
            Service = service;
            Port = string.IsNullOrWhiteSpace(port) ? "8443" : port;
            Implementation = implementation;
            Version = version;
            IsDefault = isDefault;
            HostName = string.IsNullOrWhiteSpace(hostName) ? DefaultHostName : hostName;
            PrivateKeyPem = privateKeyPem;
            CertificatePem = certificatePem;
            KeystorePassword = keystorePassword;
            KeystoreAlias = keystoreAlias;
            KeystoreFilename = keystoreFilename;
            PrivateKeyFilename = privateKeyFilename;
            PublicKeyFilename = publicKeyFilename;
        }

        public bool IsDefaultHostName => HostName == DefaultHostName;

        public string ServerXmlPath => Path.Combine(CatalinaBase, "conf", "server.xml");

        public void Validate()
        {
            if (Version.Major < 7 || Version.Major > 9)
                throw new CommandException($"Only Tomcat 7 to 9 are supported (found {Version.Major}.{Version.Minor}).");

            if (!IsDefaultHostName && !Version.UsesSslHostConfig)
                throw new CommandException("Configuring a certificate for a specific hostname (SNI) requires Tomcat 8.5 or above.");

            Implementation.ValidateVersion(Version);
        }

        /// <summary>
        /// Resolves the absolute path a keystore/key/cert file should be written to, and the value that
        /// should be written into server.xml for it - Tomcat resolves paths relative to
        /// ${catalina.base} at runtime, so an absolute path under CatalinaBase is rewritten to that
        /// portable form (matching TomcatHttpsOptions.convertPathToTomcatVariable).
        /// </summary>
        public (string AbsolutePath, string ConfigValue) ResolvePath(string configuredFilename, string defaultFileName)
        {
            var absolutePath = string.IsNullOrWhiteSpace(configuredFilename)
                ? Path.Combine(CatalinaBase, "conf", defaultFileName)
                : configuredFilename;

            var configValue = absolutePath.StartsWith(CatalinaBase)
                ? "${catalina.base}" + absolutePath.Substring(CatalinaBase.Length).Replace('\\', '/')
                : absolutePath;

            return (absolutePath, configValue);
        }
    }
}
