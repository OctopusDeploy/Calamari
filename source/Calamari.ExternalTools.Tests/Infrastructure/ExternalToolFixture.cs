using System;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    /// <summary>
    /// Base class for test fixtures that depend on an external tool.
    /// Resolves the tool via: env var override -> manifest highest (downloaded), unless
    /// CALAMARI_TOOL_SKIP_DOWNLOAD is set, in which case the tool must be found on PATH.
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

            if (ToolResolver.ShouldSkipDownload())
            {
                var pathResult = ToolResolver.FindOnPath(PrimaryToolName);
                if (pathResult == null)
                    throw new InvalidOperationException($"{ToolResolver.SkipDownloadEnvVar} was set but '{PrimaryToolName}' was not found on PATH.");

                Log($"{ToolResolver.SkipDownloadEnvVar} is set; using {PrimaryToolName} found on PATH at {pathResult}");
                ToolExecutablePath = pathResult;

                var installedVersion = ToolResolver.GetInstalledVersion(pathResult);
                if (!string.IsNullOrEmpty(installedVersion))
                {
                    ToolVersion = installedVersion;
                }
                else
                {
                    Log($"Could not determine the installed version of {PrimaryToolName} at {pathResult}; falling back to resolved version {ToolVersion}");
                }

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