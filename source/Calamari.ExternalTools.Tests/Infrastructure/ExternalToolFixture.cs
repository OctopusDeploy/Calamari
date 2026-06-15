using System;
using System.Threading.Tasks;
using System.Net.Http;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    /// <summary>
    /// Base class for test fixtures that depend on an external tool.
    /// Resolves the tool via: env var override -> PATH -> download.
    ///
    /// Subclasses set PrimaryToolName and provide a download strategy.
    /// </summary>
    public abstract class ExternalToolFixture
    {
        static readonly ToolManifest Manifest = ToolManifest.Load();

        protected string ToolExecutablePath { get; private set; } = "";
        protected string ToolVersion { get; private set; } = "";

        /// <summary>The tool name as it appears in tool-manifest.json.</summary>
        protected abstract string PrimaryToolName { get; }

        /// <summary>Download strategy for this tool. Called when the tool is not on PATH or cached.</summary>
        protected abstract Task<string> DownloadTool(string destinationDir, string version, HttpClient client);

        [OneTimeSetUp]
        public async Task ResolveTool()
        {
            var resolver = new ToolResolver(Manifest, Log);
            var downloader = new ToolDownloader(Log);

            ToolVersion = resolver.ResolveVersion(PrimaryToolName);

            // Try PATH first
            var pathResult = ToolResolver.FindOnPath(PrimaryToolName);
            if (pathResult != null)
            {
                var tool = Manifest.GetTool(PrimaryToolName);
                // For PATH tools, we use them if available (version check is best-effort)
                Log($"Found {PrimaryToolName} on PATH at {pathResult}");
                ToolExecutablePath = pathResult;
                return;
            }

            // Download and cache
            ToolExecutablePath = await downloader.Download(PrimaryToolName, ToolVersion, DownloadTool);
        }

        protected void Log(string message)
        {
            TestContext.Progress.WriteLine($"[{PrimaryToolName}] {message}");
        }
    }
}
