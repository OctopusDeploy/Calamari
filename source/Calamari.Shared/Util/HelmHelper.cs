using Calamari.Commands.Support;
using System;
using System.Diagnostics;

namespace Calamari.Util
{
    public enum HelmVersion
    {
        Version2,
        Version3
    }

    public static class HelmHelper
    {
        public static HelmVersion GetHelmVersionForDirectory(string directory, ILog log)
        {
            var versionString = InvokeWithOutput("version --client --short", directory, log, "get helm client version");
            return GetHelmVersion(versionString);
        }

        public static HelmVersion GetHelmVersion(string versionString)
        {
            //eg of output for helm 2: Client: v2.16.1+gbbdfe5e
            //eg of output for helm 3: v3.0.1+g7c22ef9

            var indexOfVersionIdentifier = versionString.IndexOf('v');
            if (indexOfVersionIdentifier == -1)
                throw new FormatException($"Failed to find version identifier from '{versionString}'.");

            var indexOfVersionNumber = indexOfVersionIdentifier + 1;
            if (indexOfVersionNumber >= versionString.Length)
                throw new FormatException($"Failed to find version number from '{versionString}'.");

            var version = versionString[indexOfVersionNumber];
            switch (version)
            {
                case '3':
                    return HelmVersion.Version3;
                case '2':
                    return HelmVersion.Version2;
                default:
                    throw new FormatException($"Unsupported helm version '{version}'");
            }
        }

        public static string InvokeWithOutput(string args, string dir, ILog log, string specificAction)
        {
            var info = new ProcessStartInfo("helm", args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = dir,
                CreateNoWindow = true
            };

            var sw = Stopwatch.StartNew();
            var timeout = TimeSpan.FromSeconds(30);
            using (var server = Process.Start(info))
            {
                if (server == null)
                    throw new CommandException("Failed to start helm process.");

                while (!server.WaitForExit(10000) && sw.Elapsed < timeout)
                {
                    log.Warn($"Still waiting for {info.FileName} {info.Arguments} [PID:{server.Id}] to exit after waiting {sw.Elapsed}...");
                }

                var stdout = server.StandardOutput.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(stdout))
                    log.Verbose(stdout);

                var stderr = server.StandardError.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(stderr))
                    log.Error(stderr);

                if (!server.HasExited)
                {
                    server.Kill();
                    throw new CommandException($"Helm failed to {specificAction} in an appropriate period of time ({timeout.TotalSeconds} sec). Please try again or check your connection.");
                }

                if (server.ExitCode != 0)
                    throw new CommandException($"Helm failed to {specificAction} (Exit code {server.ExitCode}). Error output: \r\n{stderr}");

                return stdout;
            }
        }
    }
}
