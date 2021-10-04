// Much of this class was based on code from https://github.com/NuGet/NuGet.Client. It was ported, as the NuGet libraries are .NET 4.5 and Calamari is .NET 4.0
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.//
#if USE_NUGET_V2_LIBS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using Calamari.Common.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octopus.Versioning;
using Octopus.Versioning.Semver;

namespace Calamari.Integration.Packages.NuGet
{
    internal static class NuGetV3Downloader
    {
        public static bool CanHandle(Uri feedUri, ICredentials feedCredentials, TimeSpan httpTimeout)
        {
            if (feedUri.ToString().EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            
            return IsJsonEndpoint(feedUri, feedCredentials, httpTimeout);
        }

        static bool IsJsonEndpoint(Uri feedUri, ICredentials feedCredentials, TimeSpan httpTimeout)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, feedUri);

            using (var httpClient = CreateHttpClient(feedCredentials, httpTimeout))
            {
                var sending = httpClient.SendAsync(request);
                sending.Wait();
                
                using (var response = sending.Result)
                {
                    response.EnsureSuccessStatusCode();

                    return response.Content.Headers.ContentType.MediaType == "application/json";
                }
            }
        }

        public static void DownloadPackage(string packageId, IVersion version, Uri feedUri, ICredentials feedCredentials, string targetFilePath, TimeSpan httpTimeout)
        {
            var downloadUri = DownloadUri(packageId, version, feedUri, feedCredentials, httpTimeout);

            if (downloadUri == null)
            {
                throw new InvalidOperationException($"Unable to find url to download package: {version} with version: {version} from feed: {feedUri}");
            }

            using (var nupkgFile = new FileStream(targetFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                GetHttp(downloadUri, feedCredentials, httpTimeout, pkgStream =>
                {
                    pkgStream.CopyTo(nupkgFile);
                });
            }
        }

        static Uri? DownloadUri(string packageId, IVersion version, Uri feedUri, ICredentials feedCredentials, TimeSpan httpTimeout)
        {
            var normalizedId = packageId.ToLowerInvariant();
            var normalizedVersion = version.ToString().ToLowerInvariant();
            
            var packageBaseDownloadUri = GetPackageBaseDownloadUri(feedUri, feedCredentials, httpTimeout, normalizedId, normalizedVersion, out IDictionary<string, List<Uri>> resources);
            if (packageBaseDownloadUri != null) return packageBaseDownloadUri;

            return GetPackageRegistrationDownloadUri(version, feedCredentials, httpTimeout, resources, normalizedId, normalizedVersion);
        }

        static Uri? GetPackageRegistrationDownloadUri(IVersion version, ICredentials feedCredentials, TimeSpan httpTimeout, IDictionary<string, List<Uri>> resources, string normalizedId, string normalizedVersion)
        {
            var packageRegistrationUri = GetPackageRegistrationUri(resources, normalizedId);

            var packageRegistrationResponse = GetJsonResponse(packageRegistrationUri, feedCredentials, httpTimeout);
            var packageRegistrationId = packageRegistrationResponse["@id"];

            if (packageRegistrationId.Type == JTokenType.String)
            {
                var packageRegistrationIdResponse = GetJsonResponse(new Uri(packageRegistrationId.ToString()), feedCredentials, httpTimeout);

                if (packageRegistrationIdResponse.TryGetValue("items", out JToken entries))
                {
                    foreach (var entry in entries)
                    {
                        var entryObject = entry.ToObject<JObject>();
                        var entryObjectId = entryObject["@id"];

                        if (entryObjectId == null)
                        {
                            continue;
                        }

                        var entryObjectIdResponse = GetJsonResponse(new Uri(entryObjectId.ToString()), feedCredentials, httpTimeout);

                        if (entryObjectIdResponse.TryGetValue("items", out JToken catalogItems))
                        {
                            foreach (var catalogItem in catalogItems)
                            {
                                var catalogEntry = catalogItem["catalogEntry"];

                                if (catalogEntry?["version"] != null)
                                {
                                    var packageVersion = new SemanticVersion(catalogEntry["version"].ToString());

                                    if (VersionComparer.Default.Compare(packageVersion, version) == 0)
                                    {
                                        var url = catalogEntry["packageContent"]?.ToString();
                                        if (url == null) continue;
                                        {
                                            return new Uri($"{url.Remove(url.LastIndexOfAny("/".ToCharArray()) + 1)}{normalizedVersion}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        static Uri? GetPackageBaseDownloadUri(Uri feedUri, ICredentials feedCredentials, TimeSpan httpTimeout, string normalizedId, string normalizedVersion, out IDictionary<string, List<Uri>>? resources)
        {
            
            var json = GetServiceIndexJson(feedUri, feedCredentials, httpTimeout);

            if (json == null)
            {
                throw new CommandException($"'{feedUri}' is not a valid NuGet v3 feed");
            }

            resources = GetServiceResources(json);

            var packageBaseUri = GetPackageBaseUri(resources);

            if (packageBaseUri?.AbsoluteUri.TrimEnd('/') != null)
            {
                {
                    return new Uri($"{packageBaseUri}/{normalizedId}/{normalizedVersion}/{normalizedId}.{normalizedVersion}.nupkg");
                }
            }

            return null;
        }

        static Uri GetPackageRegistrationUri(IDictionary<string, List<Uri>> resources, string normalizedId)
        {
            var registrationUrl = NuGetServiceTypes.RegistrationsBaseUrl
                                                   .Where(serviceType => resources.ContainsKey(serviceType))
                                                   .SelectMany(serviceType => resources[serviceType])
                                                   .First()
                                                   .OriginalString.TrimEnd('/');

            var packageRegistrationUri = new Uri($"{registrationUrl}/{normalizedId}/index.json");
            return packageRegistrationUri;
        }

        static HttpClient CreateHttpClient(ICredentials credentials, TimeSpan httpTimeout)
        {
            var handler = new WebRequestHandler
            {
                Credentials = credentials,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            var httpClient = new HttpClient(handler)
            {
                Timeout = httpTimeout
            };

            httpClient.DefaultRequestHeaders.Add("user-agent", "NuGet Client V3/3.4.3");

            return httpClient;
        }

        static void GetHttp(Uri uri, ICredentials credentials, TimeSpan httpTimeout, Action<Stream> processContent)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);

            using (var httpClient = CreateHttpClient(credentials, httpTimeout))
            {
                var sending = httpClient.SendAsync(request);
                sending.Wait();
                using (var response = sending.Result)
                {
                    response.EnsureSuccessStatusCode();
                    var readingStream = response.Content.ReadAsStreamAsync();
                    readingStream.Wait();
                    processContent(readingStream.Result);
                }
            }
        }

        static Uri? GetPackageBaseUri(IDictionary<string, List<Uri>> resources)
        {
            // If index.json contains a flat container resource use that to directly
            // construct package download urls.
            if (resources.ContainsKey(NuGetServiceTypes.PackageBaseAddress))
                return resources[NuGetServiceTypes.PackageBaseAddress].FirstOrDefault();
            return null;
        }

        static JObject? GetServiceIndexJson(Uri feedUri, ICredentials feedCredentials, TimeSpan httpTimeout)
        {
            var json = GetJsonResponse(feedUri, feedCredentials, httpTimeout);

            if (!IsValidV3Json(json)) return null;
            
            return json;
        }

        static JObject GetJsonResponse(Uri feedUri, ICredentials feedCredentials, TimeSpan httpTimeout)
        {
            // Parse JSON for package base URL
            JObject json = null;
            GetHttp(feedUri,
                    feedCredentials,
                    httpTimeout,
                    stream =>
                    {
                        using (var streamReader = new StreamReader(stream))
                        using (var jsonReader = new JsonTextReader(streamReader))
                        {
                            json = JObject.Load(jsonReader);
                        }
                    });
            return json;
        }

        static bool IsValidV3Json(JObject json)
        {
            // Use SemVer instead of NuGetVersion, the service index should always be
            // in strict SemVer format
            JToken versionToken;
            if (json.TryGetValue("version", out versionToken) &&
                versionToken.Type == JTokenType.String)
            {
                var version = VersionFactory.TryCreateSemanticVersion((string)versionToken);
                if (version != null && version.Major == 3)
                {
                    return true;
                }
            }
            return false;
        }

        static IDictionary<string, List<Uri>> GetServiceResources(JObject index)
        {
            var result = new Dictionary<string, List<Uri>>();

            JToken resources;
            if (index.TryGetValue("resources", out resources))
            {
                foreach (var resource in resources)
                {
                    JToken type = resource["@type"];
                    JToken id = resource["@id"];

                    if (type == null || id == null)
                    {
                        continue;
                    }

                    if (type.Type == JTokenType.Array)
                    {
                        foreach (var nType in type)
                        {
                            AddEndpoint(result, nType, id);
                        }
                    }
                    else
                    {
                        AddEndpoint(result, type, id);
                    }
                }
            }

            return result;
        }

        static void AddEndpoint(IDictionary<string, List<Uri>> result, JToken typeToken, JToken idToken)
        {
            string type = (string)typeToken;
            string id = (string)idToken;

            if (type == null || id == null)
            {
                return;
            }

            List<Uri> ids;
            if (!result.TryGetValue(type, out ids))
            {
                ids = new List<Uri>();
                result.Add(type, ids);
            }

            Uri uri;
            if (Uri.TryCreate(id, UriKind.Absolute, out uri))
            {
                ids.Add(new Uri(id));
            }
        }
    }
}
#endif