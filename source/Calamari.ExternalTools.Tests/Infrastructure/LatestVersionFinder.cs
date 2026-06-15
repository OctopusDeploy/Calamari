using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    /// <summary>
    /// Discovers the latest released version of external tools by querying their release endpoints.
    /// Each tool has a different release metadata format, so this class has per-tool scrapers.
    /// </summary>
    public static class LatestVersionFinder
    {
        static readonly HttpClient Client = new();

        public static async Task<Version> FindLatestVersion(string toolName)
        {
            return toolName switch
            {
                "terraform" => await FindLatestTerraform(),
                "kubectl" => await FindLatestKubectl(),
                "helm" => await FindLatestHelm(),
                _ => throw new NotSupportedException($"Version discovery not yet implemented for '{toolName}'")
            };
        }

        static async Task<Version> FindLatestTerraform()
        {
            // HashiCorp checkpoint API returns current version
            var response = await Client.GetStringAsync("https://checkpoint-api.hashicorp.com/v1/check/terraform");
            var doc = JsonDocument.Parse(response);
            var versionString = doc.RootElement.GetProperty("current_version").GetString();
            return Version.Parse(versionString!);
        }

        static async Task<Version> FindLatestKubectl()
        {
            // Google publishes the stable version as a plain text file
            var versionString = await Client.GetStringAsync(
                "https://storage.googleapis.com/kubernetes-release/release/stable.txt");
            return Version.Parse(versionString.Trim().TrimStart('v'));
        }

        static async Task<Version> FindLatestHelm()
        {
            // GitHub releases API for Helm
            var request = new HttpRequestMessage(HttpMethod.Get,
                "https://api.github.com/repos/helm/helm/releases/latest");
            request.Headers.UserAgent.ParseAdd("Calamari-ExternalTools");

            var response = await Client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var tagName = doc.RootElement.GetProperty("tag_name").GetString();
            return Version.Parse(tagName!.TrimStart('v'));
        }
    }
}
