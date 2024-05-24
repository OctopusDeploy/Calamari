using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    // TODO: make less static, and store things like the client.
    class Oci
    {
        const string VersionPath = "v2";
       // const string OciImageManifestAcceptHeader = "application/vnd.oci.image.manifest.v1+json";

        internal class Manifest
        {
            internal const string MediaTypePropertyName = "mediaType";
            internal const string DockerImageMediaTypeValue = "application/vnd.docker.distribution.manifest.v2+json";
            
            internal class Config
            {
                internal const string PropertyName = "config";
                internal const string MediaTypePropertyName = "mediaType";
                internal const string OciImageMediaTypeValue = "application/vnd.oci.image.config.v1+json";
                internal const string DockerImageMediaTypeValue = "application/vnd.docker.container.image.v1+json";
            }

            internal class Image
            {
                internal const string TitleAnnotationKey = "org.opencontainers.image.title";
            }

            internal class Layers
            {
                internal const string PropertyName = "layers";
                internal const string DigestPropertyName = "digest";
                internal const string SizePropertyName = "size";
                internal const string MediaTypePropertyName = "mediaType";
                internal const string AnnotationsPropertyName = "annotations";
                internal const string HelmChartMediaTypeValue = "application/vnd.cncf.helm.chart.content.v1.tar+gzip"; // https://helm.sh/docs/topics/registries/#oci-feature-deprecation-and-behavior-changes-with-v370
                internal const string DockerImageMediaTypeValue = "application/vnd.docker.image.rootfs.diff.tar.gzip";
            }
        }

        static readonly Regex PackageDigestHashRegex = new Regex(@"[A-Za-z0-9_+.-]+:(?<hash>[A-Fa-f0-9]+)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        internal static JObject? GetManifest(HttpClient client,
                                             Uri url,
                                             string packageId,
                                             string version,
                                             string? feedUsername,
                                             string? feedPassword)
        {
            using var response = Get(client, new Uri($"{url}/{packageId}/manifests/{version}"), new NetworkCredential(feedUsername, feedPassword), ApplyAcceptHeader);
            var manifest = JsonConvert.DeserializeObject<JObject>(response.Content.ReadAsStringAsync().Result);

            return manifest;
        }

        static void ApplyAcceptHeader(HttpRequestMessage request)
            => request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(Manifest.Config.OciImageMediaTypeValue));

        internal static Uri GetApiUri(Uri feedUri)
        {
            var httpScheme = BuildScheme(IsPlainHttp(feedUri));

            var r = feedUri.ToString().Replace($"oci{Uri.SchemeDelimiter}", $"{httpScheme}{Uri.SchemeDelimiter}").TrimEnd('/');
            var uri = new Uri(r);
            if (!r.EndsWith("/" + VersionPath))
            {
                uri = new Uri(uri, VersionPath);
            }

            return uri;

            static bool IsPlainHttp(Uri uri)
                => uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

            static string BuildScheme(bool isPlainHttp)
                => isPlainHttp ? Uri.UriSchemeHttp : Uri.UriSchemeHttps;
        }

        // oci registries don't support the '+' tagging
        // https://helm.sh/docs/topics/registries/#oci-feature-deprecation-and-behavior-changes-with-v380
        internal static string FixVersion(IVersion version)
            => version.ToString().Replace("+", "_");

        internal static string? GetPackageHashFromDigest(string digest)
            => PackageDigestHashRegex.Match(digest).Groups["hash"]?.Value;

        internal static HttpResponseMessage Get(HttpClient client, Uri url, ICredentials credentials, Action<HttpRequestMessage>? customAcceptHeader = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            try
            {
                var networkCredential = credentials.GetCredential(url, "Basic");

                if (!string.IsNullOrWhiteSpace(networkCredential?.UserName) || !string.IsNullOrWhiteSpace(networkCredential?.Password))
                {
                    request.Headers.Authorization = CreateAuthenticationHeader(networkCredential);
                }

                customAcceptHeader?.Invoke(request);
                var response = SendRequest(client, request);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var tokenFromAuthService = GetAuthRequestHeader(client, response, networkCredential);
                    request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = tokenFromAuthService;
                    customAcceptHeader?.Invoke(request);
                    response = SendRequest(client, request);

                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        throw new CommandException($"Authorization to `{url}` failed.");
                    }
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // Some registries do not support the Docker HTTP APIs
                    // For example GitHub: https://github.community/t/ghcr-io-docker-http-api/130121
                    throw new CommandException($"Docker registry located at `{url}` does not support this action.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorMessage = $"Request to Docker registry located at `{url}` failed with {response.StatusCode}:{response.ReasonPhrase}.";

                    var responseBody = GetContent(response);
                    if (!string.IsNullOrWhiteSpace(responseBody)) errorMessage += $" {responseBody}";

                    throw new CommandException(errorMessage);
                }

                return response;
            }
            finally
            {
                request.Dispose();
            }
        }

        static AuthenticationHeaderValue GetAuthRequestHeader(HttpClient client, HttpResponseMessage response, NetworkCredential credential)
        {
            var auth = response.Headers.WwwAuthenticate.FirstOrDefault(a => a.Scheme == "Bearer");
            if (auth != null)
            {
                var authToken = RetrieveAuthenticationToken(client, GetOAuthServiceUrl(auth), credential);
                return new AuthenticationHeaderValue("Bearer", authToken);
            }

            if (response.Headers.WwwAuthenticate.Any(a => a.Scheme == "Basic"))
            {
                return CreateAuthenticationHeader(credential);
            }

            throw new CommandException($"Unknown Authentication scheme for Uri `{response.RequestMessage.RequestUri}`");
        }

        static string RetrieveAuthenticationToken(HttpClient client, string authUrl, NetworkCredential credential)
        {
            HttpResponseMessage? response = null;

            try
            {
                using (var msg = new HttpRequestMessage(HttpMethod.Get, authUrl))
                {
                    if (credential?.UserName != null)
                    {
                        msg.Headers.Authorization = CreateAuthenticationHeader(credential);
                    }

                    response = SendRequest(client, msg);
                }

                if (response.IsSuccessStatusCode)
                {
                    return ExtractTokenFromResponse(response);
                }
            }
            finally
            {
                response?.Dispose();
            }

            throw new CommandException("Unable to retrieve authentication token required to perform operation.");
        }

        static string GetOAuthServiceUrl(AuthenticationHeaderValue auth)
        {
            var details = auth.Parameter.Split(',').ToDictionary(x => x.Substring(0, x.IndexOf('=')), y => y.Substring(y.IndexOf('=') + 1, y.Length - y.IndexOf('=') - 1).Trim('"'));
            var oathUrl = new UriBuilder(details["realm"]);
            var queryStringValues = new Dictionary<string, string>();
            if (details.TryGetValue("service", out var service))
            {
                var encodedService = WebUtility.UrlEncode(service);
                queryStringValues.Add("service", encodedService);
            }

            if (details.TryGetValue("scope", out var scope))
            {
                var encodedScope = WebUtility.UrlEncode(scope);
                queryStringValues.Add("scope", encodedScope);
            }

            oathUrl.Query = "?" + string.Join("&", queryStringValues.Select(kvp => $"{kvp.Key}={kvp.Value}").ToArray());
            return oathUrl.ToString();
        }

        /*
        public static bool HasAnnotationContaining(JObject manifest, string key, string value)
        {
            var annotations = manifest[Manifest.Annotations.PropertyName];
            return annotations is { Type: JTokenType.Object }
                   && annotations[key] != null
                   && annotations[key].ToString().IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0; // ~equiv to case insensitive contains, for non-net standard 2.0+
        }     */
        
        public static bool HasMediaTypeContaining(JObject manifest, string value)
        {
            var mediaType = manifest[Manifest.MediaTypePropertyName];

            return mediaType != null
                   && mediaType.ToString().IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool HasConfigMediaTypeContaining(JObject manifest, string value)
        {
            var config = manifest[Manifest.Config.PropertyName];

            return config is { Type: JTokenType.Object }
                   && config[Manifest.Config.MediaTypePropertyName] != null
                   && config[Manifest.Config.MediaTypePropertyName].ToString().IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool HasLayersMediaTypeContaining(JObject manifest, string value)
        {
            var layers = manifest[Manifest.Layers.PropertyName];

            if (layers is { Type: JTokenType.Array })
            {
                foreach (var layer in layers)
                {
                    if (layer[Manifest.Layers.MediaTypePropertyName] != null 
                        && layer[Manifest.Layers.MediaTypePropertyName].ToString().IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static string ExtractTokenFromResponse(HttpResponseMessage response)
        {
            var token = GetContent(response);

            var lastItem = (string)JObject.Parse(token).SelectToken("token");
            if (lastItem != null)
            {
                return lastItem;
            }

            throw new CommandException("Unable to retrieve authentication token required to perform operation.");
        }

        static AuthenticationHeaderValue CreateAuthenticationHeader(NetworkCredential credential)
        {
            var byteArray = Encoding.ASCII.GetBytes($"{credential.UserName}:{credential.Password}");
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        static HttpResponseMessage SendRequest(HttpClient client, HttpRequestMessage request)
        {
            return client.SendAsync(request).GetAwaiter().GetResult();
        }

        static string? GetContent(HttpResponseMessage response)
        {
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
    }
}