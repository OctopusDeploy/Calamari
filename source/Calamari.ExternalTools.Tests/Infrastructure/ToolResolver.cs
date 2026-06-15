using System;
using System.IO;
using System.Text;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    /// <summary>
    /// Resolves the path to an external tool using the resolution order:
    /// 1. Version override env var (CALAMARI_TOOL_{NAME}_VERSION) → download that version
    /// 2. Skip download env var (CALAMARI_TOOL_SKIP_DOWNLOAD=true) → PATH only, fail if not found
    /// 3. Default → download the manifest's highest version
    /// </summary>
    public class ToolResolver
    {
        readonly ToolManifest manifest;
        readonly Action<string> log;

        public const string SkipDownloadEnvVar = "CALAMARI_TOOL_SKIP_DOWNLOAD";

        public ToolResolver(ToolManifest manifest, Action<string> log)
        {
            this.manifest = manifest;
            this.log = log;
        }

        public static string GetOverrideEnvVar(string toolName)
        {
            return $"CALAMARI_TOOL_{toolName.Replace("-", "_").ToUpperInvariant()}_VERSION";
        }

        public static bool ShouldSkipDownload
            => string.Equals(Environment.GetEnvironmentVariable(SkipDownloadEnvVar), "true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Resolves the version to use for a tool, considering env var overrides.
        /// Returns the override version if set, otherwise the highest from manifest.
        /// </summary>
        public string ResolveVersion(string toolName)
        {
            var envVar = GetOverrideEnvVar(toolName);
            var overrideVersion = Environment.GetEnvironmentVariable(envVar);

            if (!string.IsNullOrEmpty(overrideVersion))
            {
                log($"Using override version {overrideVersion} for {toolName} (from {envVar})");
                return overrideVersion;
            }

            var tool = manifest.GetTool(toolName);
            if (tool == null)
                throw new InvalidOperationException($"Tool '{toolName}' not found in manifest");

            return tool.Highest.ToString();
        }

        /// <summary>
        /// Checks whether a tool executable exists on PATH.
        /// Returns the full path if found, null otherwise.
        /// </summary>
        public static string? FindOnPath(string toolName)
        {
            try
            {
                var command = CalamariEnvironment.IsRunningOnWindows ? "where" : "which";
                var executableName = CalamariEnvironment.IsRunningOnWindows
                    ? $"{toolName}.exe"
                    : toolName;

                var stdOut = new StringBuilder();
                var result = SilentProcessRunner.ExecuteCommand(
                    command,
                    executableName,
                    ".",
                    s => stdOut.AppendLine(s),
                    _ => { });

                if (result.ExitCode == 0)
                {
                    var path = stdOut.ToString().Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    return path.Length > 0 ? path[0] : null;
                }
            }
            catch
            {
                // Tool not found
            }

            return null;
        }

        /// <summary>
        /// Gets the version of a locally installed tool by running it with --version.
        /// Returns null if the version cannot be determined.
        /// </summary>
        public static string? GetInstalledVersion(string executablePath, string versionArg = "--version")
        {
            try
            {
                var stdOut = new StringBuilder();
                var result = SilentProcessRunner.ExecuteCommand(
                    executablePath,
                    versionArg,
                    ".",
                    s => stdOut.AppendLine(s),
                    _ => { });

                return result.ExitCode == 0 ? stdOut.ToString().Trim() : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
