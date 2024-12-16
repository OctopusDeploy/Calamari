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

namespace Calamari.Integration.Packages.Download.Oci
{
    public class OciClient
    {
        readonly HttpClient httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None });

        public JObject? GetManifest(Uri feedUri, string packageId, IVersion version, string? feedUsername, string? feedPassword)
        {
            var url = GetApiUri(feedUri);
            var fixedVersion = FixVersion(version);
            using var response = Get(new Uri($"{url}/{packageId}/manifests/{fixedVersion}"), new NetworkCredential(feedUsername, feedPassword), ApplyAcceptHeaderFunc);
            return JsonConvert.DeserializeObject<JObject>(response.Content.ReadAsStringAsync().Result);

            void ApplyAcceptHeaderFunc(HttpRequestMessage request) => request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(OciConstants.Manifest.Config.OciImageMediaTypeValue));
        }

        public HttpResponseMessage GetPackage(Uri feedUri, string packageId, string digest, string? feedUsername, string? feedPassword)
        {
            var url = GetApiUri(feedUri);
            HttpResponseMessage? response = null;
            try
            {
                response = Get(new Uri($"{url}/{packageId}/blobs/{digest}"), new NetworkCredential(feedUsername, feedPassword));

                if (!response.IsSuccessStatusCode)
                {
                    throw new CommandException($"Failed to download artifact (Status Code {(int)response.StatusCode}). Reason: {response.ReasonPhrase}");
                }

                return response;
            }
            catch
            {
                response?.Dispose();
                throw;
            }
        }

        HttpResponseMessage Get(Uri url, ICredentials credentials, Action<HttpRequestMessage>? customAcceptHeader = null)
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
                var response = SendRequest(httpClient, request);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var tokenFromAuthService = GetAuthRequestHeader(response, networkCredential);
                    request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = tokenFromAuthService;
                    customAcceptHeader?.Invoke(request);
                    response = SendRequest(httpClient, request);

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

        AuthenticationHeaderValue GetAuthRequestHeader(HttpResponseMessage response, NetworkCredential credential)
        {
            var auth = response.Headers.WwwAuthenticate.FirstOrDefault(a => a.Scheme == "Bearer");
            if (auth != null)
            {
                var authToken = RetrieveAuthenticationToken(auth, credential);
                return new AuthenticationHeaderValue("Bearer", authToken);
            }

            if (response.Headers.WwwAuthenticate.Any(a => a.Scheme == "Basic"))
            {
                return CreateAuthenticationHeader(credential);
            }

            throw new CommandException($"Unknown Authentication scheme for Uri `{response.RequestMessage.RequestUri}`");
        }

        string RetrieveAuthenticationToken(AuthenticationHeaderValue auth, NetworkCredential credential)
        {
            HttpResponseMessage? response = null;

            try
            {
                var authUrl = GetOAuthServiceUrl(auth);
                using (var msg = new HttpRequestMessage(HttpMethod.Get, authUrl))
                {
                    if (credential.UserName != null)
                    {
                        msg.Headers.Authorization = CreateAuthenticationHeader(credential);
                    }

                    response = SendRequest(httpClient, msg);
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

        static string ExtractTokenFromResponse(HttpResponseMessage response)
        {
            var token = GetContent(response);
            return (string)JObject.Parse(token).SelectToken("token")
                   ?? throw new CommandException("Unable to retrieve authentication token required to perform operation.");
        }

        static Uri GetApiUri(Uri feedUri)
        {
            var httpScheme = IsPlainHttp(feedUri) ? Uri.UriSchemeHttp : Uri.UriSchemeHttps;

            var r = feedUri.ToString().Replace($"oci{Uri.SchemeDelimiter}", $"{httpScheme}{Uri.SchemeDelimiter}").TrimEnd('/');
            var uri = new Uri(r);

            const string versionPath = "v2";
            if (!r.EndsWith("/" + versionPath))
            {
                uri = new Uri(uri, versionPath);
            }

            return uri;

            static bool IsPlainHttp(Uri uri)
                => uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        }

        // oci registries don't support the '+' tagging
        // https://helm.sh/docs/topics/registries/#oci-feature-deprecation-and-behavior-changes-with-v380
        static string FixVersion(IVersion version)
            => version.ToString().Replace("+", "_");

        static readonly Regex PackageDigestHashRegex = new Regex(@"[A-Za-z0-9_+.-]+:(?<hash>[A-Fa-f0-9]+)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        internal static string? GetPackageHashFromDigest(string digest)
            => PackageDigestHashRegex.Match(digest).Groups["hash"]?.Value;

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