using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies
{
    public static class AzureCliStrategy
    {
        public static Task<string> Download(string destinationDir, string version, HttpClient client)
        {
            var path = ToolResolver.FindOnPath("az");
            if (path != null)
                return Task.FromResult(path);

            throw new InvalidOperationException(
                "Azure CLI (az) must be installed manually. " +
                "See https://learn.microsoft.com/en-us/cli/azure/install-azure-cli");
        }
    }
}
