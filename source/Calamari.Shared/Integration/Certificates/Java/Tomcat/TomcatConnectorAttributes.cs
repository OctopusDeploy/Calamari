namespace Calamari.Integration.Certificates.Java.Tomcat
{
    /// <summary>
    /// Matches com.octopus.calamari.tomcathttps.AttributeDatabase (Octopus.Dependencies.Java) - the
    /// certificate/keystore-related Connector attribute names that must be stripped before writing new
    /// certificate configuration, so a previous APR-style config doesn't linger alongside a new
    /// keystore-style one (or vice versa).
    /// </summary>
    public static class TomcatConnectorAttributes
    {
        public const string DefaultSslHostConfigName = "defaultSSLHostConfigName";

        public static readonly string[] ConflictingAttributes =
        {
            "certificateKeyFile",
            "certificateFile",
            "certificateKeyAlias",
            "certificateKeyPassword",
            "certificateKeystoreFile",
            "certificateKeystorePassword",
            "certificateKeystoreProvider",
            "certificateKeystoreType",
            "SSLCertificateFile",
            "SSLCertificateKeyFile",
            "SSLPassword",
            "keyAlias",
            "keyPass",
            "keystoreFile",
            "keystorePass",
            "keystoreProvider",
            "keystoreType"
        };

        public static void RemoveConflictingAttributes(System.Xml.XmlElement element)
        {
            foreach (var attribute in ConflictingAttributes)
            {
                if (element.HasAttribute(attribute))
                    element.RemoveAttribute(attribute);
            }
        }
    }
}
