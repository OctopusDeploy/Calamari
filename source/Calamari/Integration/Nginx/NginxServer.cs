using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Deployment.Features;
using Calamari.Integration.FileSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Integration.Nginx
{
    public abstract class NginxServer
    {
        private readonly ICalamariFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
        
        private readonly List<KeyValuePair<string, string>> serverBindingDirectives =
            new List<KeyValuePair<string, string>>();

        private readonly string TempConfigRootDirectory = "conf";
        private readonly string TempSslRootDirectory = "ssl";

        private bool useHostName;
        private string hostName;
        private readonly IDictionary<string, string> additionalLocations = new Dictionary<string, string>();
        private readonly IDictionary<string, string> sslCerts = new Dictionary<string, string>();
        private string virtualServerName;
        private dynamic rootLocation;

        private string virtualServerConfig;

        public static NginxServer AutoDetect()
        {
            if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
                return new NixNginxServer();

            return new WindowsNginxServer();
        }

        public NginxServer WithVirtualServerName(string name)
        {
            virtualServerName = name;
            return this;
        }

        public NginxServer WithServerBindings(IEnumerable<Binding> bindings, IDictionary<string, (string SubjectCommonName, string CertificatePem, string PrivateKeyPem)> certificates)
        {
            foreach (var binding in bindings)
            {
                if (string.Equals("http", binding.Protocol, StringComparison.InvariantCultureIgnoreCase))
                {
                    AddServerBindingDirective(NginxDirectives.Server.Listen,
                        GetListenValue(binding.IpAddress, binding.Port));
                }
                else
                {
                    string certificatePath = null;
                    string certificateKeyPath = null;
                    if (string.IsNullOrEmpty(binding.CertificateLocation))
                    {
                        if (!certificates.TryGetValue(binding.CertificateVariable, out var certificate))
                        {
                            Log.Warn($"Couldn't find certificate {binding.CertificateVariable}");
                            continue;
                        }

                        var certificateRootPath = Path.Combine(TempSslRootDirectory,
                            fileSystem.RemoveInvalidFileNameChars(certificate.SubjectCommonName));

                        certificatePath = Path.Combine(certificateRootPath,
                            $"{fileSystem.RemoveInvalidFileNameChars(certificate.SubjectCommonName)}.crt");

                        certificateKeyPath = Path.Combine(certificateRootPath,
                            $"{fileSystem.RemoveInvalidFileNameChars(certificate.SubjectCommonName)}.key");

                        sslCerts.Add(certificatePath, certificate.CertificatePem);

                        sslCerts.Add(certificateKeyPath, certificate.PrivateKeyPem);
                    }

                    AddServerBindingDirective(NginxDirectives.Server.Listen,
                        GetListenValue(binding.IpAddress, binding.Port, true));

                    AddServerBindingDirective(NginxDirectives.Server.Certificate,
                        certificatePath ?? binding.CertificateLocation);

                    AddServerBindingDirective(NginxDirectives.Server.CertificateKey,
                        certificateKeyPath ?? binding.CertificateKeyLocation);

                    var securityProtocols = binding.SecurityProtocols;
                    if (securityProtocols != null && securityProtocols.Any())
                    {
                        AddServerBindingDirective(NginxDirectives.Server.SecurityProtocols, string.Join(" ", binding.SecurityProtocols));
                    }

//                    if (!string.IsNullOrWhiteSpace((string) binding.ciphers))
//                    {
//                        AddServerBindingDirective(NginxDirectives.Server.SslCiphers, string.Join(":", binding.ciphers));
//                    }
                }
            }

            return this;
        }

        public NginxServer WithHostName(string serverHostName)
        {
            if (string.IsNullOrWhiteSpace(serverHostName) || serverHostName.Equals("*")) return this;

            useHostName = true;
            hostName = serverHostName;

            return this;
        }

        public NginxServer WithAdditionalLocations(IEnumerable<Location> locations)
        {
            if (!locations.Any()) return this;

            var locationIndex = 0;
            foreach (var location in locations)
            {
                var locationConfig = GetLocationConfig(location);
                var sanitizedLocationName = SanitizeLocationName(location.Path, locationIndex.ToString());
                var locationConfFile = Path.Combine(TempConfigRootDirectory, $"{virtualServerName}.conf.d",
                    $"location.{sanitizedLocationName}.conf");

                additionalLocations.Add(locationConfFile, locationConfig);
                locationIndex++;
            }

            return this;
        }

        private string SanitizeLocationName(string locationPath, string defaultValue)
        {
            var match = Regex.Match(locationPath, "[a-zA-Z0-9/]+");
            return match.Success 
                ? match.Value.Replace("/", "_").Trim('_') 
                : defaultValue;
        }

        public NginxServer WithRootLocation(Location location)
        {
            rootLocation = location;

            return this;
        }

        public void BuildConfiguration(string customNginxConfigRoot)
        {
            var nginxConfigRootDirectory = customNginxConfigRoot ?? GetConfigRootDirectory();
            virtualServerConfig =
                $@"
server {{
{string.Join(Environment.NewLine, serverBindingDirectives.Select(binding => $"    {binding.Key} {binding.Value};"))}
{(useHostName ? $"    {NginxDirectives.Server.HostName} {hostName};" : "")}
{(additionalLocations.Any() ? $"    {NginxDirectives.Include} {nginxConfigRootDirectory}/{virtualServerName}.conf.d/location.*.conf;" : "")}
{GetLocationConfig(rootLocation)}
}}
";
        }

        public void SaveConfiguration(string tempDirectory)
        {
            foreach (var sslCert in sslCerts)
            {
                var sslCertPath = Path.Combine(tempDirectory, sslCert.Key);
                fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(sslCertPath));
                fileSystem.OverwriteFile(sslCertPath, sslCert.Value);
            }

            foreach (var additionalLocation in additionalLocations)
            {
                var locationConfPath = Path.Combine(tempDirectory, additionalLocation.Key);
                fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(locationConfPath));
                fileSystem.OverwriteFile(locationConfPath, additionalLocation.Value);
            }

            var virtualServerConfPath = Path.Combine(tempDirectory, TempConfigRootDirectory, $"{virtualServerName}.conf");
            fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(virtualServerConfPath));
            fileSystem.OverwriteFile(virtualServerConfPath, virtualServerConfig);
        }

        protected abstract string GetConfigRootDirectory();

        private string GetListenValue(string ipAddress, string port, bool isHttps = false)
        {
            var sslParameter = isHttps ? " ssl" : "";
            var ipAddressParameter =
                !string.IsNullOrWhiteSpace(ipAddress) && !ipAddress.Equals("*") ? $"{ipAddress}:" : "";

            return $"{ipAddressParameter}{port}{sslParameter}";
        }

        private string GetLocationConfig(Location location)
        {
            return
                $@"
    location {location.Path} {{
{(!string.IsNullOrEmpty(location.ReverseProxyUrl) ? $"        {NginxDirectives.Location.Proxy.Url} {location.ReverseProxyUrl};" : "")}
{GetLocationDirectives(location.Directives, location.ReverseProxyDirectives)}
{GetLocationHeaders(location.Headers, location.ReverseProxyHeaders)}
    }}
";
        }

        private static string GetLocationDirectives(string directivesString, string reverseProxyDirectivesString)
        {
            if (string.IsNullOrEmpty(directivesString) && string.IsNullOrEmpty(reverseProxyDirectivesString)) return string.Empty;

            var directives = ParseJson(directivesString);
            var reverseProxyDirectives = ParseJson(reverseProxyDirectivesString);
            var allDirectives = CombineItems(directives.ToList(), reverseProxyDirectives.ToList());
            return !allDirectives.Any()
                ? string.Empty
                : string.Join(Environment.NewLine, allDirectives.Select(d => $"        {d.Name} {d.Value};"));
        }

        private static string GetLocationHeaders(string headersString, string reverseProxyHeadersString)
        {
            if (string.IsNullOrEmpty(headersString) && string.IsNullOrEmpty(reverseProxyHeadersString)) return string.Empty;

            var headers = ParseJson(headersString);
            var reverseProxyHeaders = ParseJson(reverseProxyHeadersString);
            var allHeaders = CombineItems(headers.ToList(), reverseProxyHeaders.ToList());
            return !allHeaders.Any()
                ? string.Empty
                : string.Join(Environment.NewLine,
                    allHeaders.Select(h => $"        {NginxDirectives.Location.Proxy.SetHeader} {h.Name} {h.Value};"));
        }

        private void AddServerBindingDirective(string key, string value)
        {
            serverBindingDirectives.Add(new KeyValuePair<string, string>(key, value));
        }

        static List<dynamic> CombineItems(List<dynamic> items1, List<dynamic> items2)
        {
            if (!items1.Any()) return items2;
            
            foreach (var item in items2)
            {
                if (items1.All(i => !string.Equals(i.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    items1.Add(item);
                }
            }

            return items1;
        }
        
        static IEnumerable<dynamic> ParseJson(string json)
        {
            try
            {
                IEnumerable<dynamic> items = JObject.Parse(json);
                return items;
            }
            catch
            {
                return new List<dynamic>();
            }
        }

    }

    public class Binding
    {
        public string Protocol { get; set; }
        public string Port { get; set; }
        public string IpAddress { get; set; }
        public string CertificateLocation { get; set; }
        public string CertificateKeyLocation { get; set; }
        public string CertificateVariable { get; set; }
        public IEnumerable<string> SecurityProtocols { get; set; }
        public bool Enabled { get; set; }
    }

    public class Location
    {
        public string Path { get; set; }
        public string ReverseProxyUrl { get; set; }
        public string Directives { get; set; }
        public string Headers { get; set; }
        public string ReverseProxyHeaders { get; set; }
        public string ReverseProxyDirectives { get; set; }
    }
}