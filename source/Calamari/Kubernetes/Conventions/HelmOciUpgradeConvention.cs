using System;
using System.IO;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;

namespace Calamari.Kubernetes.Conventions
{

    public class HelmOciUpgradeConvention : IInstallConvention
    {
        readonly ILog log;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;
        readonly ICalamariFileSystem fileSystem;

        public HelmOciUpgradeConvention(ILog log, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            var feedUri = deployment.Variables.Get("Octopus.Action.Package[].Feed.Uri");
            if (string.IsNullOrWhiteSpace(feedUri) || !feedUri.StartsWith("oci", StringComparison.OrdinalIgnoreCase))
                return;
            
            ScriptSyntax syntax = ScriptSyntaxHelper.GetPreferredScriptSyntaxForEnvironment();
            var cmd = BuildHelmCommand(deployment, syntax);
            var fileName = SyntaxSpecificFileName(deployment, syntax);

            using (new TemporaryFile(fileName))
            {
                fileSystem.OverwriteFile(fileName, cmd);
                var result = scriptEngine.Execute(new Script(fileName), deployment.Variables, commandLineRunner);
                if (result.ExitCode != 0)
                {
                    throw new CommandException($"Helm Upgrade returned non-zero exit code: {result.ExitCode}. Deployment terminated.");
                }

                if (result.HasErrors && deployment.Variables.GetFlag(Deployment.SpecialVariables.Action.FailScriptOnErrorOutput, false))
                {
                    throw new CommandException("Helm Upgrade returned zero exit code but had error output. Deployment terminated.");
                }
            }
        }

        string BuildHelmCommand(RunningDeployment deployment, ScriptSyntax syntax)
        {
            var packageId = deployment.Variables.Get(PackageVariables.IndexedPackageId(string.Empty));
            var packageVersion = deployment.Variables.Get("Octopus.Action.Package.PackageVersion");
            var feedUri = deployment.Variables.Get("Octopus.Action.Package[].Feed.Uri");
            var releaseName = GetReleaseName(deployment.Variables);
            var ns = GetNamespaceParameter(deployment);

            var command = $"helm upgrade --install {releaseName} {feedUri}/{ns}/{packageId} --version {packageVersion}";
            log.Verbose(command);
            return command;

            // var helmVersion = GetVersion(deployment.Variables);
            //
            // var releaseName = GetReleaseName(deployment.Variables);
            // var packagePath = GetChartLocation(deployment);
            //
            // var customHelmExecutable = CustomHelmExecutableFullPath(deployment.Variables, deployment.CurrentDirectory);
            //
            // CheckHelmToolVersion(customHelmExecutable, helmVersion);
            //     
            // var sb = new StringBuilder();
            //
            // SetExecutable(sb, syntax, customHelmExecutable);
            // sb.Append($" upgrade --install");
            // SetNamespaceParameter(deployment, sb);
            // SetResetValuesParameter(deployment, sb);
            //
            // SetTimeoutParameter(deployment, sb);
            // SetValuesParameters(deployment, sb);
            // SetAdditionalArguments(deployment, sb);
            // sb.Append($" \"{releaseName}\" \"{packagePath}\"");
            //
            // log.Verbose(sb.ToString());
            // return sb.ToString();
        }

        string CustomHelmExecutableFullPath(IVariables variables, string workingDirectory)
        {
            var helmExecutable = variables.Get(SpecialVariables.Helm.CustomHelmExecutable);
            if (!string.IsNullOrWhiteSpace(helmExecutable))
            {
                if (variables.GetIndexes(PackageVariables.PackageCollection)
                             .Contains(SpecialVariables.Helm.Packages.CustomHelmExePackageKey)
                    && !Path.IsPathRooted(helmExecutable))
                {
                    var fullPath = Path.GetFullPath(Path.Combine(workingDirectory, SpecialVariables.Helm.Packages.CustomHelmExePackageKey, helmExecutable));
                    log.Info(
                             $"Using custom helm executable at {helmExecutable} from inside package. Full path at {fullPath}");

                    return fullPath;
                }
                else
                {
                    log.Info($"Using custom helm executable at {helmExecutable}");
                    return helmExecutable;
                }
            }

            return null;
        }

        string GetReleaseName(IVariables variables)
        {
            var validChars = new Regex("[^a-zA-Z0-9-]");
            var releaseName = variables.Get(SpecialVariables.Helm.ReleaseName)?.ToLower();
            if (string.IsNullOrWhiteSpace(releaseName))
            {
                releaseName = $"{variables.Get(ActionVariables.Name)}-{variables.Get(DeploymentEnvironment.Name)}";
                releaseName = validChars.Replace(releaseName, "").ToLowerInvariant();
            }

            log.SetOutputVariable("ReleaseName", releaseName, variables);
            log.Info($"Using Release Name {releaseName}");
            return releaseName;
        }

        static string GetNamespaceParameter(RunningDeployment deployment)
        {
            var @namespace = deployment.Variables.Get(SpecialVariables.Helm.Namespace);
            return @namespace;
        }

        string SyntaxSpecificFileName(RunningDeployment deployment, ScriptSyntax syntax)
        {
            return Path.Combine(deployment.CurrentDirectory, syntax == ScriptSyntax.PowerShell ? "Calamari.HelmUpgrade.ps1" : "Calamari.HelmUpgrade.sh");
        }


        // HelmVersion GetVersion(IVariables variables)
        // {
        //     var clientVersionText = variables.Get(SpecialVariables.Helm.ClientVersion);
        //
        //     if (Enum.TryParse(clientVersionText, out HelmVersion version))
        //         return version;
        //
        //     throw new CommandException($"Unrecognized Helm version: '{clientVersionText}'");
        // }

    }
}