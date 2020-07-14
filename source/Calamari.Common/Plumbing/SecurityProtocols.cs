using System;
using System.Net;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Common.Plumbing
{
    public static class SecurityProtocols
    {
        public static void EnableAllSecurityProtocols()
        {
            // TLS1.2 was required to access GitHub apis as of 22 Feb 2018. 
            // https://developer.github.com/changes/2018-02-01-weak-crypto-removal-notice/

            // TLS1.1 and below was discontinued on MavenCentral as of 18 June 2018
            //https://central.sonatype.org/articles/2018/May/04/discontinue-support-for-tlsv11-and-below/

            var securityProcotolTypes =
#if HAS_SSL3
                SecurityProtocolType.Ssl3 |
#endif
                SecurityProtocolType.Tls;

            // Enum.IsDefined is used as even though it may be compiled against net40 which does not have the flag
            // if at runtime it runs on say net475 the flags are present
            if (Enum.IsDefined(typeof(SecurityProtocolType), 768))
                securityProcotolTypes = securityProcotolTypes | (SecurityProtocolType)768;

            if (Enum.IsDefined(typeof(SecurityProtocolType), 3072))
                securityProcotolTypes = securityProcotolTypes | (SecurityProtocolType)3072;
            else
                Log.Verbose($"TLS1.2 is not supported, this means that some outgoing connections to third party endpoints will not work as they now only support TLS1.2.{Environment.NewLine}This includes GitHub feeds and Maven feeds.");

            ServicePointManager.SecurityProtocol = securityProcotolTypes;
        }
    }
}