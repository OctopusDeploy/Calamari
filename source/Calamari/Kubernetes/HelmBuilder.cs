using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Octostache;
using SharpCompress.Common;

namespace Calamari.Kubernetes
{
    public static class HelmBuilder
    {

        public static IHelmCommandBuilder GetHelmCommandBuilderForInstalledHelmVersion(ICalamariFileSystem fileSystem, VariableDictionary variableDictionary, string workingDirectory = "")
        {
            //var workingDirectory = fileSystem.CreateTemporaryDirectory();
            
            var info = new ProcessStartInfo(HelmExecutable(variableDictionary), "version --client --short")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory =  workingDirectory
            };
            
            var stdOutVersion = "";

            var sw = Stopwatch.StartNew();
            var timeout = TimeSpan.FromSeconds(30);
            using (var server = Process.Start(info))
            {
                while (!server.WaitForExit(10000) && sw.Elapsed < timeout)
                {
                    Log.Warn($"Still waiting for {info.FileName} {info.Arguments} [PID:{server.Id}] to exit after waiting {sw.Elapsed}...");
                }

                stdOutVersion = server.StandardOutput.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(stdOutVersion))
                {
                    Log.Verbose(stdOutVersion);
                }

                var stderr = server.StandardError.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    Log.Error(stderr);
                }

                if (!server.HasExited)
                {
                    server.Kill();
                    throw new CommandException($"Helm failed to get the version in an appropriate period of time ({timeout.TotalSeconds} sec). Please try again or check your connection.");
                }

                if (server.ExitCode != 0)
                {
                    throw new CommandException($"Helm failed to get the version (Exit code {server.ExitCode}). Error output: \r\n{stderr}");
                }
            }
            
            if (stdOutVersion.IndexOf('v') < 0)
            {
                throw new InvalidFormatException(
                    $"Version output of {stdOutVersion} cannot be parsed into helm version as it's of invalid format");
            }
            var version = stdOutVersion[stdOutVersion.IndexOf('v') + 1];

            if (version.Equals('3')) return new Helm3CommandBuilder();
            return new Helm2CommandBuilder();
            
        }

        public static String HelmExecutable(VariableDictionary variableDictionary)
        {
            var helmExecutableStringBuilder = new StringBuilder();
            var helmExecutable = variableDictionary.Get(SpecialVariables.Helm.CustomHelmExecutable);
            if (!string.IsNullOrWhiteSpace(helmExecutable))
            {
                if (variableDictionary.GetIndexes(Deployment.SpecialVariables.Packages.PackageCollection)
                        .Contains(SpecialVariables.Helm.Packages.CustomHelmExePackageKey) && !Path.IsPathRooted(helmExecutable))
                {
                    helmExecutable = Path.Combine(SpecialVariables.Helm.Packages.CustomHelmExePackageKey, helmExecutable);
                    Log.Info(
                        $"Using custom helm executable at {helmExecutable} from inside package. Full path at {Path.GetFullPath(helmExecutable)}");
                }
                else
                {
                    Log.Info($"Using custom helm executable at {helmExecutable}");
                }

                // With PowerShell we need to invoke custom executables
                helmExecutableStringBuilder.Append(ScriptSyntaxHelper.GetPreferredScriptSyntaxForEnvironment() == ScriptSyntax.PowerShell ? ". " : $"chmod +x \"{helmExecutable}\"\n");
                helmExecutableStringBuilder.Append($"\"{helmExecutable}\"");
            }
            else
            {
                helmExecutableStringBuilder.Append("helm");
            }

            return helmExecutableStringBuilder.ToString();
        }
    }
}