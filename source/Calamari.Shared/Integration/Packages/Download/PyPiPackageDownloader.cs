using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    /// <summary>
    /// Downloads packages from a PyPI-compatible Simple index (PEP 503/691).
    /// </summary>
    public class PyPiPackageDownloader : IPackageDownloader
    {
        static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();

        // PEP 427: wheel filename {name}-{version}-{python}-{abi}-{platform}.whl
        // Name is normalized (no hyphens), so version is always the second '-'-delimited segment.
        static readonly Regex WheelVersionRegex = new Regex(@"^[^-]+-([^-]+)-[^-]+-[^-]+-[^-]+\.whl$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Matches <a href="url[#fragment]">filename</a> in Simple API HTML responses
        static readonly Regex HtmlAnchorRegex = new Regex(@"<a\s[^>]*href=""([^""#]+)(?:#[^""]*)?""[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly HttpClient client;

        public PyPiPackageDownloader(ILog log, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None });
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.pypi.simple.v1+json"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html", 0.9));
        }

        public PackagePhysicalFileMetadata DownloadPackage(
            string packageId,
            IVersion version,
            string feedId,
            Uri feedUri,
            string? feedUsername,
            string? feedPassword,
            bool forcePackageDownload,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            var cacheDirectory = PackageDownloaderUtils.GetPackageRoot(feedId);
            fileSystem.EnsureDirectoryExists(cacheDirectory);

            if (!forcePackageDownload)
            {
                var cached = SourceFromCache(packageId, version, cacheDirectory);
                if (cached != null)
                {
                    log.VerboseFormat("Package was found in cache. No need to download. Using file: '{0}'", cached.FullFilePath);
                    return cached;
                }
            }

            return DownloadPackage(packageId, version, feedUri, GetFeedCredentials(feedUsername, feedPassword), cacheDirectory, maxDownloadAttempts, downloadAttemptBackoff);
        }

        PackagePhysicalFileMetadata? SourceFromCache(string packageId, IVersion version, string cacheDirectory)
        {
            log.VerboseFormat("Checking package cache for package {0} v{1}", packageId, version.ToString());

            foreach (var ext in new[] { ".whl", ".tar.gz", ".zip" })
            {
                var files = fileSystem.EnumerateFilesRecursively(cacheDirectory, PackageName.ToSearchPatterns(packageId, version, [ext]));
                foreach (var file in files)
                {
                    var package = PackageName.FromFile(file);
                    if (package == null) continue;

                    var idMatches = string.Equals(package.PackageId, packageId, StringComparison.OrdinalIgnoreCase);
                    var versionMatches = package.Version.Equals(version) ||
                                        string.Equals(package.Version.ToString(), version.ToString(), StringComparison.OrdinalIgnoreCase);

                    if (idMatches && versionMatches)
                        return PackagePhysicalFileMetadata.Build(file, package);
                }
            }

            return null;
        }

        PackagePhysicalFileMetadata DownloadPackage(
            string packageId,
            IVersion version,
            Uri feedUri,
            ICredentials feedCredentials,
            string cacheDirectory,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            Guard.NotNullOrWhiteSpace(packageId, "packageId can not be null");
            Guard.NotNull(version, "version can not be null");
            Guard.NotNull(feedUri, "feedUri can not be null");

            log.InfoFormat("Downloading PyPI package {0} v{1} from feed: '{2}'", packageId, version, feedUri);
            log.VerboseFormat("Downloaded package will be stored in: '{0}'", cacheDirectory);

            var (downloadUrl, fileExtension) = GetDownloadUrl(packageId, version, feedUri, feedCredentials, maxDownloadAttempts, downloadAttemptBackoff);

            return DownloadFile(downloadUrl, fileExtension, cacheDirectory, packageId, version, feedCredentials, maxDownloadAttempts, downloadAttemptBackoff);
        }

        (string Url, string Extension) GetDownloadUrl(
            string packageId,
            IVersion version,
            Uri feedUri,
            ICredentials feedCredentials,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            var packagePageUrl = $"{feedUri.ToString().TrimEnd('/')}/{packageId.ToLowerInvariant()}/";
            string? responseBody = null;
            string? contentType = null;

            var retryStrategy = PackageDownloaderRetryUtils.CreateRetryStrategy<HttpRequestException>(maxDownloadAttempts, downloadAttemptBackoff, log);
            retryStrategy.Execute(() =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, packagePageUrl);
                SetAuthorizationHeader(request, feedUri, feedCredentials);

                log.VerboseFormat("Fetching PyPI package file list from {0}", packagePageUrl);
                var response = client.SendAsync(request).GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"Failed to fetch PyPI package page (Status Code {(int)response.StatusCode}). Reason: {response.ReasonPhrase}");

                contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            });

            var versionString = version.OriginalString ?? version.ToString();
            var files = contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true
                ? ParseJsonResponse(responseBody!, packagePageUrl)
                : ParseHtmlResponse(responseBody!, packagePageUrl);

            // Prefer wheel, fall back to sdist
            (string url, string ext)? wheel = null;
            (string url, string ext)? sdist = null;

            foreach (var (filename, url) in files)
            {
                if (!TryParseVersionFromFilename(filename, out var fileVersion)) continue;
                if (!string.Equals(fileVersion?.OriginalString ?? fileVersion?.ToString(), versionString, StringComparison.OrdinalIgnoreCase)) continue;

                if (filename.EndsWith(".whl", StringComparison.OrdinalIgnoreCase))
                    wheel = (url, ".whl");
                else if (filename.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                    sdist ??= (url, ".tar.gz");
                else if (filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    sdist ??= (url, ".zip");
            }

            var chosen = wheel ?? sdist
                ?? throw new CommandException($"Unable to find download URL for PyPI package {packageId} version {version}");

            log.VerboseFormat("Found download URL: {0}", chosen.url);
            return chosen;
        }

        PackagePhysicalFileMetadata DownloadFile(
            string downloadUrl,
            string fileExtension,
            string cacheDirectory,
            string packageId,
            IVersion version,
            ICredentials feedCredentials,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            log.VerboseFormat("Downloading PyPI package from {0}", downloadUrl);

            var tempDirectory = fileSystem.CreateTemporaryDirectory();
            using (new TemporaryDirectory(tempDirectory))
            {
                var stagingDir = Path.Combine(tempDirectory, "staging");
                if (!Directory.Exists(stagingDir))
                    Directory.CreateDirectory(stagingDir);

                var cachedFileName = PackageName.ToCachedFileName(packageId, version, fileExtension);
                var downloadPath = Path.Combine(stagingDir, cachedFileName);

                var downloadUri = new Uri(downloadUrl);
                var retryStrategy = PackageDownloaderRetryUtils.CreateRetryStrategy<HttpRequestException>(maxDownloadAttempts, downloadAttemptBackoff, log);
                retryStrategy.Execute(() =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                    SetAuthorizationHeader(request, downloadUri, feedCredentials);

                    var response = client.SendAsync(request).GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                        throw new HttpRequestException($"Failed to download PyPI package (Status Code {(int)response.StatusCode}). Reason: {response.ReasonPhrase}");

                    using var fileStream = fileSystem.OpenFile(downloadPath, FileAccess.Write);
                    response.Content.CopyToAsync(fileStream).GetAwaiter().GetResult();
                });

                var localDownloadName = Path.Combine(cacheDirectory, cachedFileName);
                fileSystem.MoveFile(downloadPath, localDownloadName);

                return PackagePhysicalFileMetadata.Build(localDownloadName)
                    ?? throw new CommandException($"Unable to retrieve metadata for package {packageId}, version {version}");
            }
        }

        static (string Filename, string Url)[] ParseJsonResponse(string json, string packagePageUrl)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("files", out var files))
                return [];

            var results = new System.Collections.Generic.List<(string, string)>();
            foreach (var file in files.EnumerateArray())
            {
                var filename = file.TryGetProperty("filename", out var fn) ? fn.GetString() ?? string.Empty : string.Empty;
                var url = file.TryGetProperty("url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
                if (!string.IsNullOrEmpty(filename) && !string.IsNullOrEmpty(url))
                    results.Add((filename, ResolveUrl(url, packagePageUrl)));
            }
            return results.ToArray();
        }

        static (string Filename, string Url)[] ParseHtmlResponse(string html, string packagePageUrl)
        {
            var results = new System.Collections.Generic.List<(string, string)>();
            foreach (Match match in HtmlAnchorRegex.Matches(html))
            {
                var url = match.Groups[1].Value.Trim();
                var filename = match.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(filename) && !string.IsNullOrEmpty(url))
                    results.Add((filename, ResolveUrl(url, packagePageUrl)));
            }
            return results.ToArray();
        }

        static string ResolveUrl(string url, string baseUrl)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out _))
                return url;
            return new Uri(new Uri(baseUrl), url).ToString();
        }

        static bool TryParseVersionFromFilename(string filename, out IVersion? version)
        {
            version = null;

            if (filename.EndsWith(".whl", StringComparison.OrdinalIgnoreCase))
            {
                // PEP 427: {name}-{version}-{python}-{abi}-{platform}.whl — name is normalized (no hyphens)
                var parts = filename[..^4].Split('-');
                if (parts.Length >= 5)
                    return TryParseVersion(parts[1], out version);
            }
            else if (filename.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                return TryParseVersion(ExtractSdistVersion(filename[..^7]), out version);
            }
            else if (filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return TryParseVersion(ExtractSdistVersion(filename[..^4]), out version);
            }

            return false;
        }

        // Sdist: {name}-{version} — find the first segment starting with a digit
        static string? ExtractSdistVersion(string nameAndVersion)
        {
            var parts = nameAndVersion.Split('-');
            foreach (var part in parts)
                if (part.Length > 0 && char.IsDigit(part[0]))
                    return part;
            return null;
        }

        static bool TryParseVersion(string? versionString, out IVersion? version)
        {
            version = null;
            if (string.IsNullOrEmpty(versionString))
                return false;
            try
            {
                version = Octopus.Versioning.VersionFactory.CreateSemanticVersion(versionString);
                return version != null;
            }
            catch
            {
                return false;
            }
        }

        void SetAuthorizationHeader(HttpRequestMessage request, Uri uri, ICredentials feedCredentials)
        {
            var credential = feedCredentials.GetCredential(uri, "Basic");
            if (credential == null) return;

            if (string.IsNullOrWhiteSpace(credential.UserName) && !string.IsNullOrWhiteSpace(credential.Password))
            {
                // Token-only: use __token__ convention (PyPI API tokens)
                var byteArray = Encoding.ASCII.GetBytes($"__token__:{credential.Password}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }
            else if (!string.IsNullOrWhiteSpace(credential.UserName) || !string.IsNullOrWhiteSpace(credential.Password))
            {
                var byteArray = Encoding.ASCII.GetBytes($"{credential.UserName}:{credential.Password}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }
        }

        static ICredentials GetFeedCredentials(string? feedUsername, string? feedPassword)
        {
            if (!string.IsNullOrWhiteSpace(feedUsername))
                return new NetworkCredential(feedUsername, feedPassword);
            if (!string.IsNullOrWhiteSpace(feedPassword))
                return new NetworkCredential(string.Empty, feedPassword);
            return CredentialCache.DefaultNetworkCredentials;
        }
    }
}
