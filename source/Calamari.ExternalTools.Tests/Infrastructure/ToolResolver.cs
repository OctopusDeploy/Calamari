using System;
using System.Text;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    /// <summary>
    /// Resolves the version to use for a tool: env var override, else the manifest's highest.
    /// Resolution of the actual executable (PATH vs download) is done by ExternalToolFixture.
    /// </summary>
    public class ToolResolver
    {
        readonly ToolManifest manifest;
        readonly Action<string> log;

        public ToolResolver(ToolManifest manifest, Action<string> log)
        {
            this.manifest = manifest;
            this.log = log;
        }

        public static string GetOverrideEnvVar(string toolName)
        {
            return $"CALAMARI_TOOL_{toolName.Replace("-", "_").ToUpperInvariant()}_VERSION";
        }

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
