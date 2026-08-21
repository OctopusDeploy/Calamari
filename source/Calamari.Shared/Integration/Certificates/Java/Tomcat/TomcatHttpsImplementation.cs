namespace Calamari.Integration.Certificates.Java.Tomcat
{
    /// <summary>
    /// Matches com.octopus.calamari.tomcathttps.TomcatHttpsImplementation (Octopus.Dependencies.Java).
    /// NIO2 requires Tomcat 8.0+; BIO was removed in Tomcat 8.5. BIO/NIO/NIO2 all configure identically
    /// (a keystore file) - only the protocol class name and version bounds differ. APR is the odd one
    /// out: it configures raw PEM certificate/key files directly, with no keystore involved at all.
    /// </summary>
    public enum TomcatHttpsImplementation
    {
        Nio,
        Nio2,
        Bio,
        Apr
    }

    public static class TomcatHttpsImplementationExtensions
    {
        public static string ProtocolClassName(this TomcatHttpsImplementation implementation)
        {
            switch (implementation)
            {
                case TomcatHttpsImplementation.Apr:
                    return "org.apache.coyote.http11.Http11AprProtocol";
                case TomcatHttpsImplementation.Nio:
                    return "org.apache.coyote.http11.Http11NioProtocol";
                case TomcatHttpsImplementation.Nio2:
                    return "org.apache.coyote.http11.Http11Nio2Protocol";
                case TomcatHttpsImplementation.Bio:
                    return "org.apache.coyote.http11.Http11Protocol";
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(implementation));
            }
        }

        public static bool IsKeystoreBased(this TomcatHttpsImplementation implementation) => implementation != TomcatHttpsImplementation.Apr;

        public static void ValidateVersion(this TomcatHttpsImplementation implementation, TomcatVersion version)
        {
            if (implementation == TomcatHttpsImplementation.Nio2 && version.Major < 8)
                throw new Calamari.Common.Commands.CommandException("Tomcat versions before 8.0 do not support the Non-Blocking IO 2 Connector.");

            if (implementation == TomcatHttpsImplementation.Bio && (version.Major > 8 || (version.Major == 8 && version.Minor >= 5)))
                throw new Calamari.Common.Commands.CommandException("Tomcat 8.5 and above do not support the Blocking IO Connector.");
        }
    }

    public struct TomcatVersion
    {
        public int Major { get; }
        public int Minor { get; }

        public TomcatVersion(int major, int minor)
        {
            Major = major;
            Minor = minor;
        }

        /// <summary>
        /// True for Tomcat 8.5 and above (the version at which the SSLHostConfig/Certificate element
        /// style replaced setting certificate attributes directly on the Connector).
        /// </summary>
        public bool UsesSslHostConfig => Major > 8 || (Major == 8 && Minor >= 5);

        public static TomcatVersion Parse(string serverInfoOutput)
        {
            var match = System.Text.RegularExpressions.Regex.Match(serverInfoOutput, @"Server number:\s+(?<major>\d+)\.(?<minor>\d+)");
            if (!match.Success)
                throw new Calamari.Common.Commands.CommandException($"Could not determine the Tomcat version from: {serverInfoOutput}");

            return new TomcatVersion(int.Parse(match.Groups["major"].Value), int.Parse(match.Groups["minor"].Value));
        }
    }
}
