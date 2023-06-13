using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    public class OciPackageDownloader : IPackageDownloader
    {
        const string VersionPath = "v2";
        const string OciImageManifestAcceptHeader = "application/vnd.oci.image.manifest.v1+json";
        const string ManifestImageTitleAnnotationKey = "org.opencontainers.image.title";
        const string ManifestLayerPropertyName = "layers";
        const string ManifestLayerDigestPropertyName = "digest";
        const string ManifestLayerSizePropertyName = "size";
        const string ManifestLayerAnnotationsPropertyName = "annotations";
        const string ManifestLayerMediaTypePropertyName = "mediaType";

        static Regex PackageDigestHashRegex = new Regex(@"[A-Za-z0-9_+.-]+:(?<hash>[A-Fa-f0-9]+)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
        static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();
        readonly ICalamariFileSystem fileSystem;
        readonly ICombinedPackageExtractor combinedPackageExtractor;
        readonly HttpClient client;

        public OciPackageDownloader(
            ICalamariFileSystem fileSystem,
            ICombinedPackageExtractor combinedPackageExtractor)
        {
            this.fileSystem = fileSystem;
            this.combinedPackageExtractor = combinedPackageExtractor;
            client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None });
        }

        public PackagePhysicalFileMetadata DownloadPackage(string packageId,
            IVersion version,
            string feedId,
            Uri feedUri,
            string? feedUsername,
            string? feedPassword,
            bool forcePackageDownload,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            var cacheDirectory = PackageDownloaderUtils.GetPackageRoot(feedId);
            fileSystem.EnsureDirectoryExists(cacheDirectory);

            if (!forcePackageDownload)
            {
                var downloaded = SourceFromCache(packageId, version, cacheDirectory);
                if (downloaded != null)
                {
                    Log.VerboseFormat("Package was found in cache. No need to download. Using file: '{0}'", downloaded.FullFilePath);
                    return downloaded;
                }
            }

            var tempDirectory = fileSystem.CreateTemporaryDirectory();

            using (new TemporaryDirectory(tempDirectory))
            {
                var homeDir = Path.Combine(tempDirectory, "oci");
                if (!Directory.Exists(homeDir))
                {
                    Directory.CreateDirectory(homeDir);
                }

                var stagingDir = Path.Combine(homeDir, "staging");
                if (!Directory.Exists(stagingDir))
                {
                    Directory.CreateDirectory(stagingDir);
                }

                var versionString = FixVersion(version);

                var apiUrl = GetApiUri(feedUri);
                var (digest, size, extension) = GetPackageDetails(apiUrl, packageId, versionString, feedUsername, feedPassword);
                var hash = GetPackageHashFromDigest(digest);

                var cachedFileName = PackageName.ToCachedFileName(packageId, version, extension);
                var downloadPath = Path.Combine(Path.Combine(stagingDir, cachedFileName));

                DownloadPackage(apiUrl, packageId, digest, feedUsername, feedPassword, downloadPath);

                var localDownloadName = Path.Combine(cacheDirectory, cachedFileName);
                fileSystem.MoveFile(downloadPath, localDownloadName);

                return !string.IsNullOrEmpty(hash)
                    ? new PackagePhysicalFileMetadata(
                        PackageName.FromFile(localDownloadName),
                        localDownloadName,
                        hash,
                        size)
                    : PackagePhysicalFileMetadata.Build(localDownloadName) 
                      ?? throw new CommandException($"Unable to retrieve metadata for package {packageId}, version {version}");
            }
        }

        // oci registries don't support the '+' tagging
        // https://helm.sh/docs/topics/registries/#oci-feature-deprecation-and-behavior-changes-with-v380
        static string FixVersion(IVersion version)
            => version.ToString().Replace("+", "_");

        static string? GetPackageHashFromDigest(string digest)
            => PackageDigestHashRegex.Match(digest).Groups["hash"]?.Value;

        (string digest, int size, string extension) GetPackageDetails(
            Uri url,
            string packageId,
            string version,
            string? feedUserName, 
            string? feedPassword)
        {
            using var response = Get(new Uri($"{url}/{packageId}/manifests/{version}"), new NetworkCredential(feedUserName, feedPassword), ApplyAccept);
            var manifest = JsonConvert.DeserializeObject<JObject>(response.Content.ReadAsStringAsync().Result);

            var layer = manifest.Value<JArray>(ManifestLayerPropertyName)[0];
            var digest = layer.Value<string>(ManifestLayerDigestPropertyName);
            var size = layer.Value<int>(ManifestLayerSizePropertyName);
            var extension = GetExtensionFromManifest(layer);

            return (digest, size, extension);
        }

        string GetExtensionFromManifest(JToken layer)
        {
            var artifactTitle = layer.Value<JObject>(ManifestLayerAnnotationsPropertyName)?[ManifestImageTitleAnnotationKey]?.Value<string>() ?? "";
            var extension = combinedPackageExtractor
                .Extensions
                .FirstOrDefault(ext => 
                    Path.GetExtension(artifactTitle).Equals(ext, StringComparison.OrdinalIgnoreCase));

            return extension ?? (layer.Value<string>(ManifestLayerMediaTypePropertyName).EndsWith("tar+gzip") ? ".tgz" : ".tar");
        }

        void DownloadPackage(
            Uri url,
            string packageId,
            string digest,
            string? feedUsername,
            string? feedPassword,
            string downloadPath)
        {
            using var fileStream = fileSystem.OpenFile(downloadPath, FileAccess.Write);
            using var response = Get(new Uri($"{url}/{packageId}/blobs/{digest}"), new NetworkCredential(feedUsername, feedPassword));
            if (!response.IsSuccessStatusCode)
            {
                throw new CommandException(
                    $"Failed to download artifact (Status Code {(int)response.StatusCode}). Reason: {response.ReasonPhrase}");
            }

#if NET40
            response.Content.CopyToAsync(fileStream).Wait();
#else
            response.Content.CopyToAsync(fileStream).GetAwaiter().GetResult();
#endif
        }

        static void ApplyAccept(HttpRequestMessage request)
            => request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(OciImageManifestAcceptHeader));

        static Uri GetApiUri(Uri feedUri)
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

        PackagePhysicalFileMetadata? SourceFromCache(string packageId, IVersion version, string cacheDirectory)
        {
            Log.VerboseFormat("Checking package cache for package {0} v{1}", packageId, version.ToString());

            var files = fileSystem.EnumerateFilesRecursively(cacheDirectory, PackageName.ToSearchPatterns(packageId, version, combinedPackageExtractor.Extensions));

            foreach (var file in files)
            {
                var package = PackageName.FromFile(file);
                if (package == null)
                    continue;

                if (string.Equals(package.PackageId, packageId, StringComparison.OrdinalIgnoreCase) && package.Version.Equals(version))
                {
                    var packagePhysicalFileMetadata = PackagePhysicalFileMetadata.Build(file, package)
                                                      ?? throw new CommandException($"Unable to retrieve metadata for package {packageId}, version {version}");
                    return packagePhysicalFileMetadata;
                }
            }

            return null;
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
                var response = SendRequest(request);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var tokenFromAuthService = GetAuthRequestHeader(response, networkCredential);
                    request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = tokenFromAuthService;
                    customAcceptHeader?.Invoke(request);
                    response = SendRequest(request);

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

        string RetrieveAuthenticationToken(string authUrl, NetworkCredential credential)
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

                    response = SendRequest(msg);
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

        AuthenticationHeaderValue GetAuthRequestHeader(HttpResponseMessage response, NetworkCredential credential)
        {
            var auth = response.Headers.WwwAuthenticate.FirstOrDefault(a => a.Scheme == "Bearer");
            if (auth != null)
            {
                var authToken = RetrieveAuthenticationToken(GetOAuthServiceUrl(auth), credential);
                return new AuthenticationHeaderValue("Bearer", authToken);
            }

            if (response.Headers.WwwAuthenticate.Any(a => a.Scheme == "Basic"))
            {
                return CreateAuthenticationHeader(credential);
            }

            throw new CommandException($"Unknown Authentication scheme for Uri `{response.RequestMessage.RequestUri}`");
        }

        static string GetOAuthServiceUrl(AuthenticationHeaderValue auth)
        {
            var details = auth.Parameter.Split(',').ToDictionary(x => x.Substring(0, x.IndexOf('=')), y => y.Substring(y.IndexOf('=') + 1, y.Length - y.IndexOf('=') - 1).Trim('"'));
            var oathUrl = new UriBuilder(details["realm"]);
            var queryStringValues = new Dictionary<string, string>();
            if (details.TryGetValue("service", out var service))
            {
                queryStringValues.Add("service", HttpUtility.UrlEncode(service));
            }

            if (details.TryGetValue("scope", out var scope))
            {
                queryStringValues.Add("scope", HttpUtility.UrlEncode(scope));
            }

            oathUrl.Query = "?" + string.Join("&", queryStringValues.Select(kvp => $"{kvp.Key}={kvp.Value}").ToArray());
            return oathUrl.ToString();
        }

        static string ExtractTokenFromResponse(HttpResponseMessage response)
        {
            var token = GetContent(response);

            var lastItem = (string) JObject.Parse(token).SelectToken("token");
            if (lastItem != null)
            {
                return lastItem;
            }

            throw new CommandException("Unable to retrieve authentication token required to perform operation.");
        }

        AuthenticationHeaderValue CreateAuthenticationHeader(NetworkCredential credential)
        {
            var byteArray = Encoding.ASCII.GetBytes($"{credential.UserName}:{credential.Password}");
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        HttpResponseMessage SendRequest(HttpRequestMessage request)
        {
#if NET40
            return client.SendAsync(request).Wait();
#else
            return client.SendAsync(request).GetAwaiter().GetResult();
#endif
        }

        static string? GetContent(HttpResponseMessage response)
        {
#if NET40
            return response.Content.ReadAsStringAsync().Wait();
#else
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
#endif
        }
    }
}