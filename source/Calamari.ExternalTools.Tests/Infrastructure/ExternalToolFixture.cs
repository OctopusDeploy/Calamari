using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    /// <summary>
    /// Base class for test fixtures that depend on an external tool.
    /// Resolves the tool via: env var override -> PATH -> download.
    /// Subclasses set PrimaryToolName and provide a download strategy.
    /// </summary>
    public abstract class ExternalToolFixture
    {
        static readonly ToolManifest Manifest = ToolManifest.Load();

        protected string ToolExecutablePath { get; private set; } = "";
        protected string ToolVersion { get; private set; } = "";

        protected abstract string PrimaryToolName { get; }

        protected abstract Task<string> DownloadTool(string destinationDir, string version, HttpClient client);

        [OneTimeSetUp]
        public async Task ResolveTool()
        {
            var resolver = new ToolResolver(Manifest, Log);
            var downloader = new ToolDownloader(Log);

            ToolVersion = resolver.ResolveVersion(PrimaryToolName);

            var pathResult = ToolResolver.FindOnPath(PrimaryToolName);
            if (pathResult != null)
            {
                Log($"Found {PrimaryToolName} on PATH at {pathResult}");
                ToolExecutablePath = pathResult;
                return;
            }

            ToolExecutablePath = await downloader.Download(PrimaryToolName, ToolVersion, DownloadTool);
        }

        protected void Log(string message)
        {
            TestContext.Progress.WriteLine($"[{PrimaryToolName}] {message}");
        }
    }
}