using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Packages.NuGet;
using Newtonsoft.Json.Linq;
using NuGet;
using Octopus.CoreUtilities.Extensions;
using Octopus.Versioning;
using HttpClient = System.Net.Http.HttpClient;
using PackageName = Calamari.Common.Features.Packages.PackageName;

namespace Calamari.Integration.Packages.Download
{
    public class ArtifactoryPackageDownloader : IPackageDownloader
    {
        static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();

        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly HttpClient client;

        public ArtifactoryPackageDownloader(ILog log, ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
            this.log = log;
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
                var downloaded = AttemptToGetPackageFromCache(packageId, version, cacheDirectory);
                if (downloaded != null)
                {
                    Log.VerboseFormat("Package was found in cache. No need to download. Using file: '{0}'", downloaded.FullFilePath);
                    return downloaded;
                }
            }

            return DownloadPackage(packageId,
                                   version,
                                   feedUri,
                                   GetFeedCredentials(feedUsername, feedPassword),
                                   cacheDirectory);
        }

        PackagePhysicalFileMetadata? AttemptToGetPackageFromCache(string packageId, IVersion version, string cacheDirectory)
        {
            Log.VerboseFormat("Checking package cache for package {0} v{1}", packageId, version.ToString());

            var files = fileSystem.EnumerateFilesRecursively(cacheDirectory, PackageName.ToSearchPatterns(packageId, version));

            foreach (var file in files)
            {
                var package = PackageName.FromFile(file);
                if (package == null)
                    continue;

                var idMatches = string.Equals(package.PackageId, packageId, StringComparison.OrdinalIgnoreCase);
                var versionExactMatch = string.Equals(package.Version.ToString(), version.ToString(), StringComparison.OrdinalIgnoreCase);
                var nugetVerMatches = package.Version.Equals(version);

                if (idMatches && (nugetVerMatches || versionExactMatch))
                    return PackagePhysicalFileMetadata.Build(file, package);
            }

            return null;
        }

        public PackagePhysicalFileMetadata DownloadPackage(
            string packageId,
            IVersion version,
            Uri feedUri,
            ICredentials feedCredentials,
            string cacheDirectory)
        {
            Log.Info("Downloading package {0} v{1} from feed: '{2}'", packageId, version, feedUri);
            Log.VerboseFormat("Downloaded package will be stored in: '{0}'", cacheDirectory);

            var packagePathTokens = packageId.Split('/');
            var packagePath = packagePathTokens.Length > 0 ? Path.Combine(packagePathTokens.Take(packagePathTokens.Length - 1).ToArray()) : "";
            var packageName = packagePathTokens.Last();

            var packageFileName = GetPackageFileName(feedUri,
                                                     packagePath,
                                                     packageName,
                                                     version,
                                                     feedCredentials);
            var extension = PackageName.TryMatchTarExtensions(packageFileName, out var fileName, out var ext)
                ? ext
                : Path.GetExtension(packageFileName);

            var cachedFileName = PackageName.ToCachedFileName(packageId, version, extension);

            var tempDirectory = fileSystem.CreateTemporaryDirectory();
            using (new TemporaryDirectory(tempDirectory))
            {
                var stagingDir = Path.Combine(tempDirectory, "staging");
                if (!Directory.Exists(stagingDir))
                {
                    Directory.CreateDirectory(stagingDir);
                }

                var downloadPath = Path.Combine(Path.Combine(stagingDir, cachedFileName));

                using (var fileStream = fileSystem.OpenFile(downloadPath, FileAccess.Write))
                {
                    var packagePathWithTrailingSlash = packagePath.IsNullOrEmpty() ? "" : $"{packagePath}/";
                    using var response = Get(new Uri($"{feedUri}artifactory/{packagePathWithTrailingSlash}/{packageFileName}"), feedCredentials);
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

                var localDownloadName = Path.Combine(cacheDirectory, cachedFileName);
                fileSystem.MoveFile(downloadPath, localDownloadName);

                return PackagePhysicalFileMetadata.Build(localDownloadName);
            }
        }

        string GetPackageFileName(Uri feedUri,
                                  string packagePath,
                                  string packageName,
                                  IVersion version,
                                  ICredentials feedCredentials)
        {
            // e.g. https: //packages.octopushq.com/artifactory/api/storage/helm
            var extension = "";
            using var response = Get(new Uri($"{feedUri}artifactory/api/storage/{packagePath}"), feedCredentials);
            if (!response.IsSuccessStatusCode)
            {
                throw new CommandException(
                                           $"Failed to download artifact (Status Code {(int)response.StatusCode}). Reason: {response.ReasonPhrase}");
            }

            var respJson = "";
#if NET40
            respJson = response.Content.ReadAsStringAsync().Result;
#else
            respJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
#endif
            var packages = GetRepoChildren(respJson);
            var matchingPackages = packages
                                   .Where(p => p.Contains(packageName))
                                   .Where(p => p.Contains(version.ToString()))
                                   .ToList();
            if (matchingPackages.Count != 1)
            {
                throw new Exception($"Expected one package to match {packageName} with version {version} but found {matchingPackages.Count}. Matching packages {string.Join(",", matchingPackages)}");
            }

            return matchingPackages.FirstOrDefault();
        }

        List<string> GetRepoChildren(string json)
        {
            var childrenArrayObject = JObject.Parse(json).SelectToken("children");
            var children = ((JArray)childrenArrayObject)
                           .Where(t => t.SelectToken("folder").ToString() == "False")
                           .Select(t => t.SelectToken("uri").ToString());

            return children.ToList();
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
                        var responseBody = GetContent(response);
                        var errorMessage = $"Authorization to `{url}` failed.";
                        if (!string.IsNullOrWhiteSpace(responseBody)) errorMessage += $" {responseBody}";
                        throw new Exception(errorMessage);
                    }
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new CommandException($"Artifactory registry located at `{url}` does not support this action.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorMessage = $"Request to Artifactory located at `{url}` failed with {response.StatusCode}:{response.ReasonPhrase}.";
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
#if NET40
                var encodedScope = System.Web.HttpUtility.UrlEncode(service);
#else
                var encodedScope = WebUtility.UrlEncode(service);
#endif
            }

            if (details.TryGetValue("scope", out var scope))
            {
#if NET40
                var encodedScope = System.Web.HttpUtility.UrlEncode(scope);
#else
                var encodedScope = WebUtility.UrlEncode(scope);
#endif
            }

            oathUrl.Query = "?" + string.Join("&", queryStringValues.Select(kvp => $"{kvp.Key}={kvp.Value}").ToArray());
            return oathUrl.ToString();
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

        static string ExtractTokenFromResponse(HttpResponseMessage response)
        {
            var token = GetContent(response);

            var lastItem = (string)JObject.Parse(token).SelectToken("token");
            if (lastItem != null)
            {
                return lastItem;
            }

            throw new Exception("Unable to retrieve authentication token required to perform operation.");
        }

        static string? GetContent(HttpResponseMessage response)
        {
#if NET40
            return response.Content.ReadAsStringAsync().Result;
#else
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
#endif
        }

        AuthenticationHeaderValue CreateAuthenticationHeader(NetworkCredential credential)
        {
            var byteArray = Encoding.ASCII.GetBytes($"{credential.UserName}:{credential.Password}");
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        HttpResponseMessage SendRequest(HttpRequestMessage request)
        {
#if NET40
            return client.SendAsync(request).Result;
#else
            return client.SendAsync(request).GetAwaiter().GetResult();
#endif
        }

        static ICredentials GetFeedCredentials(string? feedUsername, string? feedPassword)
        {
            ICredentials credentials = CredentialCache.DefaultNetworkCredentials;
            if (!String.IsNullOrWhiteSpace(feedUsername))
            {
                credentials = new NetworkCredential(feedUsername, feedPassword);
            }

            return credentials;
        }
    }
}