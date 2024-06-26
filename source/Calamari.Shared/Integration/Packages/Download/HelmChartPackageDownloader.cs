﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Packages.Download.Helm;
using Octopus.Versioning;
using HttpClient = System.Net.Http.HttpClient;
using PackageName = Calamari.Common.Features.Packages.PackageName;

namespace Calamari.Integration.Packages.Download
{
    public class HelmChartPackageDownloader: IPackageDownloader
    {
        static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();
        const string Extension = ".tgz";
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly IHelmEndpointProxy endpointProxy;
        readonly HttpClient client;

        public HelmChartPackageDownloader(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
            client = new HttpClient(new HttpClientHandler{ AutomaticDecompression  = DecompressionMethods.None });
            endpointProxy = new HelmEndpointProxy(client);
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

            var feedCredentials = GetFeedCredentials(feedUsername, feedPassword);

            var strategy = PackageDownloaderRetryUtils.CreateRetryStrategy<Exception>(maxDownloadAttempts, downloadAttemptBackoff, log);
            var package = strategy.Execute(() => GetChartDetails(feedUri, feedCredentials, packageId, CancellationToken.None));

            if (string.IsNullOrEmpty(package.PackageId))
            {
                throw new CommandException($"There was an error fetching the chart from the provided repository. The package id was not valid ({package.PackageId})");
            }

            var packageVersion = package.Versions.FirstOrDefault(v => version.Equals(v.Version));
            var foundUrl = packageVersion?.Urls.FirstOrDefault();

            if (foundUrl == null)
            {
                throw new CommandException("Could not determine download url from chart repository. Please check associated index.yaml is correct.");
            }

            var packageUrl = foundUrl.IsValidUrl() ? new Uri(foundUrl, UriKind.Absolute) :  new Uri(feedUri, foundUrl);
            return DownloadChart(packageUrl, packageId, version, feedCredentials, cacheDirectory, maxDownloadAttempts, downloadAttemptBackoff);
        }

        (string PackageId, IEnumerable<HelmIndexYamlReader.ChartData> Versions) GetChartDetails(Uri feedUri, ICredentials credentials, string packageId, CancellationToken cancellationToken)
        {
            var cred = credentials.GetCredential(feedUri, "basic");

            var yaml = endpointProxy.Get(feedUri, cred.UserName, cred.Password, cancellationToken);
            var package = HelmIndexYamlReader.Read(yaml).FirstOrDefault(p => p.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            return package;
        }

        PackagePhysicalFileMetadata DownloadChart(Uri url,
                                                  string packageId,
                                                  IVersion version,
                                                  ICredentials feedCredentials,
                                                  string cacheDirectory,
                                                  int maxDownloadAttempts,
                                                  TimeSpan downloadAttemptBackoff)
        {
            var cred = feedCredentials.GetCredential(url, "basic");
            var tempDirectory = fileSystem.CreateTemporaryDirectory();

            using (new TemporaryDirectory(tempDirectory))
            {   
                var homeDir = Path.Combine(tempDirectory, "helm");
                if (!Directory.Exists(homeDir))
                {
                    Directory.CreateDirectory(homeDir);
                }
                var stagingDir = Path.Combine(tempDirectory, "staging");
                if (!Directory.Exists(stagingDir))
                {
                    Directory.CreateDirectory(stagingDir);
                }

                var cachedFileName = PackageName.ToCachedFileName(packageId, version, Extension);
                var downloadPath = Path.Combine(Path.Combine(stagingDir, cachedFileName));

                var strategy = PackageDownloaderRetryUtils.CreateRetryStrategy<CommandException>(maxDownloadAttempts, downloadAttemptBackoff, log);
                strategy.Execute(() =>
                                 {

                                     var request = new HttpRequestMessage(HttpMethod.Get, url);
                                     request.Headers.AddAuthenticationHeader(cred.UserName, cred.Password);

                                     using var fileStream = fileSystem.OpenFile(downloadPath, FileAccess.Write);
                                     using var response = client.SendAsync(request).GetAwaiter().GetResult();
                                     if (!response.IsSuccessStatusCode)
                                     {
                                         throw new CommandException($"Helm failed to download the chart (Status Code {(int)response.StatusCode}). Reason: {response.ReasonPhrase}");
                                     }
                                         
                                     response.Content.CopyToAsync(fileStream).GetAwaiter().GetResult();
                                 });

                var localDownloadName = Path.Combine(cacheDirectory, cachedFileName);

                fileSystem.MoveFile(downloadPath, localDownloadName);
                var packagePhysicalFileMetadata = PackagePhysicalFileMetadata.Build(localDownloadName);
                return packagePhysicalFileMetadata
                    ?? throw new CommandException($"Unable to retrieve metadata for package {packageId}, version {version}");
            }
        }

        PackagePhysicalFileMetadata? SourceFromCache(string packageId, IVersion version, string cacheDirectory)
        {
            Log.VerboseFormat("Checking package cache for package {0} v{1}", packageId, version.ToString());

            var files = fileSystem.EnumerateFilesRecursively(cacheDirectory, PackageName.ToSearchPatterns(packageId, version, new [] { Extension }));

            foreach (var file in files)
            {
                var package = PackageName.FromFile(file);
                if (package == null)
                    continue;

                if (string.Equals(package.PackageId, packageId, StringComparison.OrdinalIgnoreCase) && package.Version.Equals(version))
                    return PackagePhysicalFileMetadata.Build(file, package);
            }

            return null;
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