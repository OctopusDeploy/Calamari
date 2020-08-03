using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.FileSystem;
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

        public bool UseHostName { get; private set; }
        public string HostName { get; private set; }
        public IDictionary<string, string> AdditionalLocations { get; } = new Dictionary<string, string>();
        public  IDictionary<string, string> SslCerts { get; } = new Dictionary<string, string>();
        public string VirtualServerName { get; private set; }
        public dynamic RootLocation { get; private set; }

        string virtualServerConfig;

        public static NginxServer AutoDetect()
        {
            if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
                return new NixNginxServer();

            return new WindowsNginxServer();
        }

        public NginxServer WithVirtualServerName(string name)
        {
            /*
             * Ensure package ids with chars that are invalid for file names (for example, a GitHub package is in the format
             * "owner/repository") do not generate unexpected file names.
             */
            VirtualServerName = String.Join("_", name.Split(
                System.IO.Path.GetInvalidFileNameChars(),
                StringSplitOptions.RemoveEmptyEntries));
            return this;
        }

        public NginxServer WithServerBindings(IEnumerable<Binding> bindings, IDictionary<string, (string SubjectCommonName, string CertificatePem, string PrivateKeyPem)> certificates, string customSslCertRoot = null)
        {
            foreach (var binding in bindings)
            {
                if (string.Equals("http", binding.Protocol, StringComparison.OrdinalIgnoreCase))
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

                        var sanitizedSubjectCommonName =
                            fileSystem.RemoveInvalidFileNameChars(certificate.SubjectCommonName);
                        
                        var certificateFileName = $"{sanitizedSubjectCommonName}.crt";
                        var certificateKeyFileName = $"{sanitizedSubjectCommonName}.key";

                        var certificateTempRootPath = Path.Combine(TempSslRootDirectory, sanitizedSubjectCommonName);
                        SslCerts.Add(
                            Path.Combine(certificateTempRootPath, certificateFileName),
                            certificate.CertificatePem);
                        SslCerts.Add(
                            Path.Combine(certificateTempRootPath, certificateKeyFileName),
                            certificate.PrivateKeyPem);

                        var sslRootPath = customSslCertRoot ?? GetSslRootDirectory();
                        var certificateRootPath = Path.Combine(sslRootPath, sanitizedSubjectCommonName);
                        certificatePath = Path.Combine(certificateRootPath, certificateFileName);
                        certificateKeyPath = Path.Combine(certificateRootPath, certificateKeyFileName);
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
                        AddServerBindingDirective(
                            NginxDirectives.Server.SecurityProtocols,
                            string.Join(" ", binding.SecurityProtocols));
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

            UseHostName = true;
            HostName = serverHostName;

            return this;
        }

        public NginxServer WithAdditionalLocations(IEnumerable<Location> locations)
        {
            if (!locations.Any()) return this;

            var locationIndex = 0;
            foreach (var location in locations)
            {
                var locationConfig = GetLocationConfig(location);
                var sanitizedLocationName = SanitizeLocationName(location.Path, locationIndex);
                var locationConfFile = Path.Combine(TempConfigRootDirectory, $"{VirtualServerName}.conf.d",
                    $"location.{sanitizedLocationName}.conf");

                AdditionalLocations.Add(locationConfFile, locationConfig);
                locationIndex++;
            }

            return this;
        }

        private string SanitizeLocationName(string locationPath, int index)
        {
            /*
             * The names of the files holding locations are significant as Nginx will process regular expression
             * locations in the order they are defined or imported. This is from the documentation at
             * http://nginx.org/en/docs/http/request_processing.html:
             *
             * nginx first searches for the most specific prefix location given by literal strings regardless of the
             * listed order. In the configuration above the only prefix location is “/” and since it matches any request
             * it will be used as a last resort.
             *
             * [THIS IS THE IMPORTANT BIT]
             * Then nginx checks locations given by regular expression in the order listed in the configuration file.
             * The first matching expression stops the search and nginx will use this location.
             *
             * If no regular expression matches a request, then nginx uses the most specific prefix location
             * found earlier.
             *
             * To accomodate this ordering, we prefix all location paths with the index of the location as it appeared
             * in the UI. This ensures location file names are unique and processed in the order they were defined.
             */
            
            var match = Regex.Match(locationPath, "[a-zA-Z0-9/]+");
            if (match.Success)
            {
                // Remove slashes, as these are not valid for a file name
                return index + match.Value.Replace("/", "_").Trim('_');
            }

            // Fall back to the index as a file name
            return index.ToString();
        }

        public NginxServer WithRootLocation(Location location)
        {
            RootLocation = location;

            return this;
        }

        public void BuildConfiguration(string customNginxConfigRoot = null)
        {
            var nginxConfigRootDirectory = customNginxConfigRoot ?? GetConfigRootDirectory();
            virtualServerConfig = $@"
server {{
{string.Join(Environment.NewLine, serverBindingDirectives.Select(binding => $"    {binding.Key} {binding.Value};"))}
{(UseHostName ? $"    {NginxDirectives.Server.HostName} {HostName};" : "")}
{(AdditionalLocations.Any() ? $"    {NginxDirectives.Include} {nginxConfigRootDirectory}/{VirtualServerName}.conf.d/location.*.conf;" : "")}
{GetLocationConfig(RootLocation)}
}}
";
        }

        public void SaveConfiguration(string tempDirectory)
        {
            foreach (var sslCert in SslCerts)
            {
                var sslCertPath = Path.Combine(tempDirectory, sslCert.Key);
                fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(sslCertPath));
                fileSystem.OverwriteFile(sslCertPath, sslCert.Value);
            }

            foreach (var additionalLocation in AdditionalLocations)
            {
                var locationConfPath = Path.Combine(tempDirectory, additionalLocation.Key);
                fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(locationConfPath));
                fileSystem.OverwriteFile(locationConfPath, RemoveEmptyLines(additionalLocation.Value));
            }

            var virtualServerConfPath = Path.Combine(tempDirectory, TempConfigRootDirectory, $"{VirtualServerName}.conf");
            fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(virtualServerConfPath));
            fileSystem.OverwriteFile(virtualServerConfPath, RemoveEmptyLines(virtualServerConfig));
        }

        string RemoveEmptyLines(string text)
        {
            return Regex.Replace(text, @"^\s*$\n|\r$", string.Empty, RegexOptions.Multiline).TrimEnd();
        }

        public abstract string GetConfigRootDirectory();
        public abstract string GetSslRootDirectory();

        private string GetListenValue(string ipAddress, string port, bool isHttps = false)
        {
            var sslParameter = isHttps ? " ssl" : "";
            var ipAddressParameter =
                !string.IsNullOrWhiteSpace(ipAddress) && !ipAddress.Equals("*") ? $"{ipAddress}:" : "";

            return $"{ipAddressParameter}{port}{sslParameter}";
        }

        private string GetLocationConfig(Location location)
        {
            return $@"
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

            var directives = ParseJsonArray(directivesString);
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

        static IEnumerable<dynamic> ParseJsonArray(string json)
        {
            try
            {
                var result = new List<dynamic>();
                var array = JArray.Parse(json);
                foreach (var o in array.Children<JObject>())
                {
                    foreach (var p in o.Properties())
                    {
                        result.Add(new { p.Name, Value = (string)p.Value });
                    }
                }

                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return new List<dynamic>();
            }
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