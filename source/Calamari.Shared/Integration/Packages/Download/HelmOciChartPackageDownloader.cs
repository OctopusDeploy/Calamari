using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    public class HelmOciChartPackageDownloader : IPackageDownloader
    {
        readonly IScriptEngine scriptEngine;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;
        readonly IVariables variables;
        readonly ILog log;

        // Ensures that any credential details are only available for the duration of the acquisition
        readonly Dictionary<string, string> environmentVariables = new Dictionary<string, string>()
        {
            {
                "DOCKER_CONFIG", "./octo-docker-configs"
            }
        };

        
        public HelmOciChartPackageDownloader(
            IScriptEngine scriptEngine, 
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner,
            IVariables variables,
            ILog log)
        {
            this.scriptEngine = scriptEngine;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.variables = variables;
            this.log = log;
        }

        public PackagePhysicalFileMetadata DownloadPackage(string packageId,
                                                           IVersion version,
                                                           string feedId,
                                                           Uri feedUri,
                                                           string? feedUsername,
                                                           string? feedPassword,
                                                           bool forcePackageDownload,
                                                           int maxDownloadAttempts,
                                                           TimeSpan downloadAttemptBackoff)
        {
            var versionString = FixVersion(version);
            PerformLogin(feedUsername, feedPassword, feedUri.ToString());
            var fullImageName = GetFullImageName(packageId, feedUri);
            PerformPull(fullImageName, versionString);
            var hash = GetImageHash(fullImageName, versionString);
            return new PackagePhysicalFileMetadata(new PackageFileNameMetadata(packageId, version, ""), string.Empty, hash, 0);
        }

        string FixVersion(IVersion version)
        {
            // oci registries don't support the '+' tagging
            // https://helm.sh/docs/topics/registries/#oci-feature-deprecation-and-behavior-changes-with-v380
            return version.ToString().Replace("+", "_");
        }

        static string GetFullImageName(string packageId, Uri feedUri)
        {
            return $"{feedUri.Authority}{feedUri.AbsolutePath.TrimEnd('/')}/{packageId}";
        }
        
        string GetImageHash(string fullImageName, string version)
        {
            var details = "";
            var result2 = SilentProcessRunner.ExecuteCommand("helm",
                                                             $"show crds {fullImageName} --version {version}",
                                                             ".", environmentVariables, (stdout) => { details = stdout; }, log.Error);
            if (result2.ExitCode != 0)
            {
                throw new CommandException("Unable to determine acquired docker image hash");
            }


            var digest = Regex.Match(details, @"Digest: (?<hash>sha256:\w+)");
            return digest.Groups.Count == 0 ? null : digest.Groups["hash"].Value;
        }

        
        void PerformLogin(string? username, string? password, string feed)
        {
            var result = ExecuteScript("HelmOciLogin", new Dictionary<string, string?>
            {
                ["HelmUsername"] = username,
                ["HelmPassword"] = password,
                ["FeedUri"] = feed
            });
            if (result == null)
                throw new CommandException("Null result attempting to log in Helm registry");
            if (result.ExitCode != 0)
                throw new CommandException("Unable to log in Helm registry");
        }
        
        void PerformPull(string fullImageName, string version)
        {
            var result = ExecuteScript("HelmOciPull", new Dictionary<string, string?>
            {
                ["Image"] = fullImageName,
                ["Version"] = version.ToString()
            });
            if (result == null)
                throw new CommandException("Null result attempting to pull Docker image");
            if (result.ExitCode != 0)
                throw new CommandException("Unable to pull Docker image");
        }
        
        CommandResult ExecuteScript(string scriptName, Dictionary<string, string?> envVars)
        {
            var file = GetScript(scriptName);
            using (new TemporaryFile(file))
            {
                var clone = variables.Clone();
                foreach (var keyValuePair in envVars)
                {
                    clone[keyValuePair.Key] = keyValuePair.Value;
                }
                return scriptEngine.Execute(new Script(file), clone, commandLineRunner, environmentVariables);
            }
        }
        
        string GetScript(string scriptName)
        {
            var syntax = ScriptSyntaxHelper.GetPreferredScriptSyntaxForEnvironment();

            string contextFile = syntax switch
                                 {
                                     ScriptSyntax.Bash => $"{scriptName}.sh",
                                     ScriptSyntax.PowerShell => $"{scriptName}.ps1",
                                     _ => throw new InvalidOperationException("No helm oci context wrapper exists for " + syntax)
                                 };

            var scriptFile = Path.Combine(".", $"Octopus.{contextFile}");
            var contextScript = new AssemblyEmbeddedResources().GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), $"{typeof (HelmOciChartPackageDownloader).Namespace}.Scripts.{contextFile}");
            fileSystem.OverwriteFile(scriptFile, contextScript);
            return scriptFile;
        }
    }
}