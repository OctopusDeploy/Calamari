using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Tests.LaunchTools
{
    [TestFixture]
    public class TempTest
    {
        [Test]
        public async Task Temp()
        {
            const string requiredVersion = "v0.5.3";
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Octopus");
            var json = await client.GetAsync($"https://api.github.com/repos/kubernetes-sigs/aws-iam-authenticator/releases/tags/{requiredVersion}");
            json.EnsureSuccessStatusCode();
            var jObject = JObject.Parse(await json.Content.ReadAsStringAsync());
            var downloadUrl = jObject["assets"].Children().FirstOrDefault(token => token["name"].Value<string>().EndsWith("_linux_amd64"))?["browser_download_url"].Value<string>();
            var version = jObject["tag_name"].Value<string>();
        }
    }
}
