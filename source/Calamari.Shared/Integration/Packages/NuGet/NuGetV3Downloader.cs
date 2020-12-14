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
using Calamari.Common.Plumbing.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.NuGet
{
    internal static class NuGetV3Downloader
    {
        public static void DownloadPackage(string packageId, IVersion version, Uri feedUri, ICredentials feedCredentials, string targetFilePath, TimeSpan httpTimeout)
        {
            var normalizedId = packageId.ToLowerInvariant();
            var normalizedVersion = version.ToString().ToLowerInvariant();
            var packageBaseUri = GetPackageBaseUri(feedUri, feedCredentials, httpTimeout).AbsoluteUri.TrimEnd('/');
            var downloadUri = new Uri($"{packageBaseUri}/{normalizedId}/{normalizedVersion}/{normalizedId}.{normalizedVersion}.nupkg");

            Log.Verbose($"Downloading package from '{downloadUri}'");

            using (var nupkgFile = new FileStream(targetFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                GetHttp(downloadUri, feedCredentials, httpTimeout, pkgStream =>
                {
                    pkgStream.CopyTo(nupkgFile);
                });
            }
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

        static Uri GetPackageBaseUri(Uri feedUri, ICredentials feedCredentials, TimeSpan httpTimeout)
        {
            // Parse JSON for package base URL
            JObject json = null;
            GetHttp(feedUri, feedCredentials, httpTimeout, stream =>
            {
                using (var streamReader = new StreamReader(stream))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    json = JObject.Load(jsonReader);
                }
            });

            if (!IsValidV3Json(json))
                throw new CommandException($"'{feedUri}' is not a valid NuGet v3 feed");

            var resources = GetServiceResources(json);

            // If index.json contains a flat container resource use that to directly
            // construct package download urls.
            if (resources.ContainsKey(NuGetServiceTypes.PackageBaseAddress))
                return resources[NuGetServiceTypes.PackageBaseAddress].FirstOrDefault();

            return NuGetServiceTypes.RegistrationsBaseUrl
                .Where(serviceType => resources.ContainsKey(serviceType))
                .SelectMany(serviceType => resources[serviceType])
                .First();
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