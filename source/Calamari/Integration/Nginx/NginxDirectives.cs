using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Integration.Nginx
{
    public static class NginxDirectives
    {
        public static string Include = "include";

        public static class Server
        {
            public static string Listen = "listen";
            public static string HostName = "server_name";
            public static string Certificate = "ssl_certificate";
            public static string CertificateKey = "ssl_certificate_key";
            public static string SecurityProtocols = "ssl_protocols";
            public static string SslCiphers = "ssl_ciphers";
        }

        public static class Location
        {
            public static class Proxy
            {
                public static string Url = "proxy_pass";
                public static string SetHeader = "proxy_set_header";
            }
        }
    }
}
