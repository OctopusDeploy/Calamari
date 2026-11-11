using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Integration.Certificates.Java.Tomcat
{
    /// <summary>
    /// Configures a Tomcat HTTPS connector by editing server.xml directly, replacing
    /// com.octopus.calamari.tomcathttps.TomcatHttpsConfig/ConfigureConnector/ConfigureTomcat7Connector/
    /// ConfigureTomcat85Connector (Octopus.Dependencies.Java). No JVM is required - this is plain XML
    /// manipulation, and the keystore itself is built with <see cref="JavaKeystoreBuilder"/> rather
    /// than a JVM's own KeyStore API.
    /// </summary>
    public class TomcatConnectorConfigurator
    {
        readonly JavaKeystoreBuilder keystoreBuilder;
        readonly ILog log;

        public TomcatConnectorConfigurator(ILog log)
        {
            this.log = log;
            keystoreBuilder = new JavaKeystoreBuilder(log);
        }

        public void ConfigureHttps(TomcatCertificateOptions options)
        {
            options.Validate();

            if (!File.Exists(options.ServerXmlPath))
                throw new CommandException($"Could not find the Tomcat configuration file at \"{options.ServerXmlPath}\".");

            var document = new XmlDocument();
            document.Load(options.ServerXmlPath);

            var serviceNode = FindElement(document.DocumentElement!, "Service", new Dictionary<string, string> { { "name", options.Service } })
                              ?? throw new CommandException($"Could not find a <Service name=\"{options.Service}\"> element in \"{options.ServerXmlPath}\".");

            var connectorNode = FindElement(serviceNode, "Connector", new Dictionary<string, string> { { "port", options.Port } })
                                ?? CreateElement(document, serviceNode, "Connector", new Dictionary<string, string> { { "port", options.Port } });

            if (options.Version.UsesSslHostConfig)
                ConfigureTomcat85Connector(document, connectorNode, options);
            else
                ConfigureTomcat7Connector(document, connectorNode, options);

            BackupServerXml(options);
            document.Save(options.ServerXmlPath);

            log.Info($"Successfully configured HTTPS on the \"{options.Service}\" service's connector on port {options.Port}.");
        }

        // ----- Tomcat 7.0/8.0: certificate attributes go directly on <Connector> -----

        void ConfigureTomcat7Connector(XmlDocument document, XmlElement connector, TomcatCertificateOptions options)
        {
            if (options.Implementation == TomcatHttpsImplementation.Nio2)
                throw new CommandException("Tomcat 7.0/8.0 does not support the Non-Blocking IO 2 Connector.");

            ValidateProtocolSwap(connector, options);
            TomcatConnectorAttributes.RemoveConflictingAttributes(connector);

            connector.SetAttribute("protocol", options.Implementation.ProtocolClassName());
            connector.SetAttribute("SSLEnabled", "true");
            connector.SetAttribute("scheme", "https");
            connector.SetAttribute("secure", "true");

            if (options.Implementation == TomcatHttpsImplementation.Apr)
            {
                var (_, keyConfigValue) = WritePrivateKeyPem(options);
                var (_, certConfigValue) = WritePublicCertificatePem(options);
                connector.SetAttribute("SSLCertificateKeyFile", keyConfigValue);
                connector.SetAttribute("SSLCertificateFile", certConfigValue);
            }
            else
            {
                var (_, keystoreConfigValue) = WriteKeystore(options);
                connector.SetAttribute("keystoreFile", keystoreConfigValue);
                connector.SetAttribute("keystorePass", options.KeystorePassword);
                connector.SetAttribute("keyAlias", options.KeystoreAlias);
            }
        }

        // ----- Tomcat 8.5/9.0: certificate attributes go on a <SSLHostConfig><Certificate> child -----

        void ConfigureTomcat85Connector(XmlDocument document, XmlElement connector, TomcatCertificateOptions options)
        {
            ValidateProtocolSwap(connector, options);

            connector.SetAttribute("protocol", options.Implementation.ProtocolClassName());
            connector.SetAttribute("SSLEnabled", "true");

            if (DefaultHostIsAlreadyOnConnector(connector, options))
            {
                // The existing default certificate is expressed the old way (attributes directly on
                // <Connector>, with no matching <SSLHostConfig>) - keep mutating it there, rather than
                // introducing a second, conflicting way of expressing the same default host.
                TomcatConnectorAttributes.RemoveConflictingAttributes(connector);

                if (options.Implementation == TomcatHttpsImplementation.Apr)
                {
                    var (_, keyConfigValue) = WritePrivateKeyPem(options);
                    var (_, certConfigValue) = WritePublicCertificatePem(options);
                    connector.SetAttribute("SSLCertificateKeyFile", keyConfigValue);
                    connector.SetAttribute("SSLCertificateFile", certConfigValue);
                }
                else
                {
                    var (_, keystoreConfigValue) = WriteKeystore(options);
                    connector.SetAttribute("keystoreFile", keystoreConfigValue);
                    connector.SetAttribute("keystorePass", options.KeystorePassword);
                    connector.SetAttribute("keyAlias", options.KeystoreAlias);
                }

                return;
            }

            SetDefaultHostNameIfRequired(connector, options);

            var hostConfigAttrs = options.IsDefaultHostName
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { { "hostName", options.HostName } };

            var sslHostConfig = FindElement(connector, "SSLHostConfig", hostConfigAttrs, allowMissingAttributes: options.IsDefaultHostName)
                                 ?? CreateElement(document, connector, "SSLHostConfig", hostConfigAttrs);

            var certificateType = MapKeyAlgorithmToCertificateType(options);
            var certificateAttrs = new Dictionary<string, string> { { "type", certificateType } };
            var certificate = FindElement(sslHostConfig, "Certificate", certificateAttrs, allowMissingAttributes: true)
                              ?? CreateElement(document, sslHostConfig, "Certificate", certificateAttrs);

            TomcatConnectorAttributes.RemoveConflictingAttributes(certificate);

            if (options.Implementation == TomcatHttpsImplementation.Apr)
            {
                var (_, keyConfigValue) = WritePrivateKeyPem(options);
                var (_, certConfigValue) = WritePublicCertificatePem(options);
                certificate.SetAttribute("certificateKeyFile", keyConfigValue);
                certificate.SetAttribute("certificateFile", certConfigValue);
            }
            else
            {
                var (_, keystoreConfigValue) = WriteKeystore(options);
                certificate.SetAttribute("certificateKeystoreFile", keystoreConfigValue);
                certificate.SetAttribute("certificateKeystorePassword", options.KeystorePassword);
                certificate.SetAttribute("certificateKeyAlias", options.KeystoreAlias);
            }
        }

        // ----- Shared helpers -----

        static void ValidateProtocolSwap(XmlElement connector, TomcatCertificateOptions options)
        {
            if (ConnectorIsEmpty(connector))
                return;

            var currentProtocol = connector.GetAttribute("protocol");
            var newProtocol = options.Implementation.ProtocolClassName();
            if (!string.IsNullOrEmpty(currentProtocol) && currentProtocol != newProtocol)
                throw new CommandException($"The connector on port {options.Port} already has certificate configuration using a different implementation ({currentProtocol}). Remove the existing configuration before switching to {newProtocol}.");
        }

        /// <summary>
        /// Approximates com.octopus.calamari.tomcathttps.ConfigureConnector.connectorIsEmpty: true when
        /// the connector carries none of the certificate/keystore attributes this class manages, and
        /// has no SSLHostConfig children - i.e. it's a fresh connector this class hasn't touched yet.
        /// </summary>
        static bool ConnectorIsEmpty(XmlElement connector)
        {
            if (TomcatConnectorAttributes.ConflictingAttributes.Any(connector.HasAttribute))
                return false;

            return !connector.ChildNodes.OfType<XmlElement>().Any(e => e.Name == "SSLHostConfig");
        }

        bool DefaultHostIsAlreadyOnConnector(XmlElement connector, TomcatCertificateOptions options)
        {
            if (!options.IsDefaultHostName)
                return false;

            var currentDefaultHost = connector.HasAttribute(TomcatConnectorAttributes.DefaultSslHostConfigName)
                ? connector.GetAttribute(TomcatConnectorAttributes.DefaultSslHostConfigName)
                : TomcatCertificateOptions.DefaultHostName;

            if (currentDefaultHost != options.HostName)
                return false;

            var hasMatchingSslHostConfig = connector.ChildNodes.OfType<XmlElement>()
                                                     .Any(e => e.Name == "SSLHostConfig" && (!e.HasAttribute("hostName") || e.GetAttribute("hostName") == options.HostName));

            return !hasMatchingSslHostConfig && !ConnectorIsEmpty(connector);
        }

        static void SetDefaultHostNameIfRequired(XmlElement connector, TomcatCertificateOptions options)
        {
            if (!options.IsDefault && !ConnectorIsEmpty(connector))
                return;

            if (options.IsDefaultHostName)
                connector.RemoveAttribute(TomcatConnectorAttributes.DefaultSslHostConfigName);
            else
                connector.SetAttribute(TomcatConnectorAttributes.DefaultSslHostConfigName, options.HostName);
        }

        static string MapKeyAlgorithmToCertificateType(TomcatCertificateOptions options)
        {
            var algorithm = JavaKeystoreBuilder.GetPrivateKeyAlgorithm(options.PrivateKeyPem);
            switch (algorithm)
            {
                case "RSA":
                    return "RSA";
                case "DSA":
                    return "DSA";
                case "EC":
                case "ECDSA":
                    return "EC";
                default:
                    throw new CommandException($"Unrecognised private key algorithm \"{algorithm}\" - expected RSA, DSA or EC. See https://tomcat.apache.org/tomcat-8.5-doc/config/http.html#SSL_Support_-_Certificate");
            }
        }

        (string AbsolutePath, string ConfigValue) WriteKeystore(TomcatCertificateOptions options)
        {
            var (absolutePath, configValue) = options.ResolvePath(options.KeystoreFilename, "octopus.p12");
            keystoreBuilder.SaveKeystoreToFile(options.KeystoreAlias, options.PrivateKeyPem, options.CertificatePem, options.KeystorePassword, absolutePath);
            return (absolutePath, configValue);
        }

        (string AbsolutePath, string ConfigValue) WritePrivateKeyPem(TomcatCertificateOptions options)
        {
            var (absolutePath, configValue) = options.ResolvePath(options.PrivateKeyFilename, "octopus.key");
            File.WriteAllText(absolutePath, options.PrivateKeyPem);
            return (absolutePath, configValue);
        }

        (string AbsolutePath, string ConfigValue) WritePublicCertificatePem(TomcatCertificateOptions options)
        {
            var (absolutePath, configValue) = options.ResolvePath(options.PublicKeyFilename, "octopus.crt");
            File.WriteAllText(absolutePath, options.CertificatePem);
            return (absolutePath, configValue);
        }

        static void BackupServerXml(TomcatCertificateOptions options)
        {
            var backupZipPath = Path.Combine(options.CatalinaBase, "conf", "octopus_backup.zip");
            var entryName = DateTime.UtcNow.ToString("yyyy.MM.dd-HH.mm.ss.fff");

            var mode = File.Exists(backupZipPath) ? ZipArchiveMode.Update : ZipArchiveMode.Create;
            using (var fileStream = new FileStream(backupZipPath, mode == ZipArchiveMode.Create ? FileMode.CreateNew : FileMode.Open))
            using (var archive = new ZipArchive(fileStream, mode))
            {
                var entry = archive.CreateEntry($"server.xml.{entryName}");
                using (var entryStream = entry.Open())
                using (var serverXmlStream = File.OpenRead(options.ServerXmlPath))
                {
                    serverXmlStream.CopyTo(entryStream);
                }
            }
        }

        static XmlElement? FindElement(XmlElement parent, string tagName, Dictionary<string, string> requiredAttributes, bool allowMissingAttributes = false)
        {
            foreach (var child in parent.ChildNodes.OfType<XmlElement>().Where(e => e.Name == tagName))
            {
                var matches = requiredAttributes.All(attr =>
                                                          allowMissingAttributes
                                                              ? !child.HasAttribute(attr.Key) || child.GetAttribute(attr.Key) == attr.Value
                                                              : child.GetAttribute(attr.Key) == attr.Value);
                if (matches)
                    return child;
            }

            return null;
        }

        static XmlElement CreateElement(XmlDocument document, XmlElement parent, string tagName, Dictionary<string, string> attributes)
        {
            var element = document.CreateElement(tagName);
            foreach (var attribute in attributes)
                element.SetAttribute(attribute.Key, attribute.Value);

            parent.AppendChild(element);
            return element;
        }
    }
}
