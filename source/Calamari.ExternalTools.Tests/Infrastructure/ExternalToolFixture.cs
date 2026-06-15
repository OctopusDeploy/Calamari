using System;
using System.Threading.Tasks;
using System.Net.Http;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    /// <summary>
    /// Base class for test fixtures that depend on an external tool.
    ///
    /// Resolution order:
    /// 1. Version override env var (CALAMARI_TOOL_{NAME}_VERSION) → download that version
    /// 2. CALAMARI_TOOL_SKIP_DOWNLOAD=true → use PATH only, fail if not found
    /// 3. Default → download the manifest's highest version
    /// </summary>
    public abstract class ExternalToolFixture
    {
        static readonly ToolManifest Manifest = ToolManifest.Load();

        public string ToolExecutablePath { get; private set; } = "";
        public string ToolVersion { get; private set; } = "";

        /// <summary>The tool name as it appears in tool-manifest.json.</summary>
        protected abstract string PrimaryToolName { get; }

        /// <summary>Download strategy for this tool. Called when the tool is not on PATH or cached.</summary>
        protected abstract Task<string> DownloadTool(string destinationDir, string version, HttpClient client);

        [OneTimeSetUp]
        public async Task ResolveTool()
        {
            var resolver = new ToolResolver(Manifest, Log);
            ToolVersion = resolver.ResolveVersion(PrimaryToolName);

            var hasVersionOverride = !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable(ToolResolver.GetOverrideEnvVar(PrimaryToolName)));

            // Version override always triggers a download of that specific version
            if (hasVersionOverride)
            {
                Log($"Version override set — downloading {PrimaryToolName} {ToolVersion}");
                var downloader = new ToolDownloader(Log);
                ToolExecutablePath = await downloader.Download(PrimaryToolName, ToolVersion, DownloadTool);
                return;
            }

            // Skip download mode — PATH only, fail if not found
            if (ToolResolver.ShouldSkipDownload)
            {
                var pathResult = ToolResolver.FindOnPath(PrimaryToolName);
                if (pathResult == null)
                    throw new InvalidOperationException(
                        $"{PrimaryToolName} not found on PATH. " +
                        $"Either install it or unset {ToolResolver.SkipDownloadEnvVar} to allow downloading.");

                Log($"Using {PrimaryToolName} from PATH at {pathResult} (download skipped)");
                ToolExecutablePath = pathResult;
                return;
            }

            // Default — download the manifest's highest version
            Log($"Downloading {PrimaryToolName} {ToolVersion} from manifest");
            var dl = new ToolDownloader(Log);
            ToolExecutablePath = await dl.Download(PrimaryToolName, ToolVersion, DownloadTool);
        }

        protected void Log(string message)
        {
            TestContext.Progress.WriteLine($"[{PrimaryToolName}] {message}");
        }
    }
}
