using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing;
using Calamari.Testing;
using Newtonsoft.Json.Linq;

namespace Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies
{
    public static class AwsIamAuthenticatorStrategy
    {
        public static async Task<string> Download(string destinationDir, string version, HttpClient client)
        {
            var token = await ExternalVariables.Get(ExternalVariable.GitHubRateLimitingPersonalAccessToken, CancellationToken.None);
            client.DefaultRequestHeaders.UserAgent.TryParseAdd("Octopus");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", token);

            var response = await client.GetAsync(
                $"https://api.github.com/repos/kubernetes-sigs/aws-iam-authenticator/releases/tags/{version}");
            response.EnsureSuccessStatusCode();

            var jObject = JObject.Parse(await response.Content.ReadAsStringAsync());
            var fileNameEndsWith = GetFileNameEndsWith();
            var downloadUrl = jObject["assets"]!
                .Children()
                .FirstOrDefault(token => token["name"]!.Value<string>()!.EndsWith(fileNameEndsWith))?[
                    "browser_download_url"]!
                .Value<string>();

            if (downloadUrl == null)
                throw new InvalidOperationException($"Could not find aws-iam-authenticator asset ending with '{fileNameEndsWith}' in release {version}");

            var fileName = GetFileName();
            var destPath = Path.Combine(destinationDir, fileName);
            await ToolDownloader.DownloadFile(downloadUrl, destPath, client);

            return destPath;
        }

        static string GetFileNameEndsWith()
        {
            if (CalamariEnvironment.IsRunningOnNix)
                return "_linux_amd64";
            if (CalamariEnvironment.IsRunningOnMac)
                return "_darwin_amd64";

            return "_windows_amd64.exe";
        }

        static string GetFileName()
        {
            if (CalamariEnvironment.IsRunningOnWindows)
                return "aws-iam-authenticator.exe";

            return "aws-iam-authenticator";
        }
    }
}
