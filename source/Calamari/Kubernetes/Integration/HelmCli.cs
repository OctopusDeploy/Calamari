using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripting.Bash;
using Calamari.Common.Features.Scripting.WindowsPowerShell;
using Calamari.Common.Features.Scripts;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using Octopus.Versioning;
using Octopus.Versioning.Semver;

namespace Calamari.Kubernetes.Integration
{
    public class HelmCli : CommandLineTool
    {
        readonly ICommandLineRunner commandLineRunner;
        readonly ICalamariFileSystem fileSystem;
        readonly IVariables variables;
        bool isCustomExecutable;

        public HelmCli(ILog log, ICommandLineRunner commandLineRunner, RunningDeployment runningDeployment, ICalamariFileSystem fileSystem)
            : base(log, commandLineRunner, runningDeployment.CurrentDirectory, runningDeployment.EnvironmentVariables)
        {
            this.commandLineRunner = commandLineRunner;
            this.fileSystem = fileSystem;
            variables = runningDeployment.Variables;
            ExecutableLocation = SetExecutable();
        }

        string SetExecutable()
        {
            var helmExecutable = variables.Get(SpecialVariables.Helm.CustomHelmExecutable);
            if (string.IsNullOrWhiteSpace(helmExecutable))
            {
                //no custom exe, just return standard helm
                return "helm";
            }
            
            isCustomExecutable = true;

            if (variables.GetIndexes(PackageVariables.PackageCollection)
                         .Contains(SpecialVariables.Helm.Packages.CustomHelmExePackageKey)
                && !Path.IsPathRooted(helmExecutable))
            {
                var fullPath = Path.GetFullPath(Path.Combine(workingDirectory, SpecialVariables.Helm.Packages.CustomHelmExePackageKey, helmExecutable));
                log.Info($"Using custom helm executable at {helmExecutable} from inside package. Full path at {fullPath}");

                return fullPath;
            }

            log.Info($"Using custom helm executable at {helmExecutable}");
            return helmExecutable;
        }
        string NamespaceArg()
        {
            var @namespace = variables.Get(SpecialVariables.Helm.Namespace);
            return !string.IsNullOrWhiteSpace(@namespace) ? $"--namespace {@namespace}" : null;
        }
        
        public (int ExitCode, string InfoOutput) GetExecutableVersion()
        {
            var result = ExecuteCommandAndReturnOutput("version", "--client", "--short");
            return (result.Result.ExitCode, result.Output.MergeInfoLogs());
        }

        public SemanticVersion GetParsedExecutableVersion()
        {
            var (exitCode, infoOutput) = GetExecutableVersion();

            if (exitCode != 0)
            {
                log.Warn("Unable to retrieve the Helm tool version");
                return null;
            }
            
            //helm v3 output looks like: v3.14.2+gc309b6f
            var vStripped = infoOutput.TrimStart('v');

            return SemVerFactory.CreateVersion(vStripped);
        }

        public int? GetCurrentRevision(string releaseName)
        {
            var result = ExecuteCommandAndReturnOutput("get", "metadata", releaseName, "-o json", NamespaceArg());


            //if we get _any_ error back, assume it probably hasn't been installed yet
            if (result.Result.ExitCode != 0)
                return null; //
            
            //parse the output
            var json = result.Output.MergeInfoLogs();
            var metadata = JsonConvert.DeserializeAnonymousType(json,
                                                                new
                                                                {
                                                                    //we only care about parsing the revision
                                                                    revision = 0
                                                                });

            //the next revision 
            return metadata.revision;
        }

        public string GetManifest(string releaseName, int revisionNumber)
        {
            var result = ExecuteCommandAndReturnOutput("get", "manifest", releaseName, $"--revision {revisionNumber}", NamespaceArg());
            result.Result.VerifySuccess();

            return result.Output.MergeInfoLogs();
        }
        
        public CommandResult Upgrade(string releaseName, string packagePath, IEnumerable<string> upgradeArgs)
        {
            var buildArgs = new List<string>
            {
                "upgrade",
                "--install"
            };

            buildArgs.AddRange(upgradeArgs);
            buildArgs.Add(NamespaceArg());
            buildArgs.Add(releaseName);
            buildArgs.Add(packagePath);

            if (OctopusFeatureToggles.ExecuteHelmUpgradeCommandViaShellScriptFeatureToggle.IsEnabled(variables))
            {
                log.Warn("The current workaround for backwards compatibility with Helm command execution via shell scripts is temporary and will be removed in Octopus version 2025.3. To ensure continued compatibility, please update your step to provide arguments directly compatible with Helm, avoiding shell-specific formatting");
                return ExecuteCommandViaScript(buildArgs);
            }

            var result = ExecuteCommandAndLogOutput(buildArgs);
            return result;
        }

        CommandResultWithOutput ExecuteCommandAndReturnOutput(params string[] arguments)
        {
            ChmodExecutable();
            return base.ExecuteCommandAndReturnOutput(ExecutableLocation, SanitiseCommandLineArgs(arguments));
        }
        
        CommandResult ExecuteCommandAndLogOutput(IEnumerable<string> arguments)
        {
            ChmodExecutable();
            return base.ExecuteCommandAndLogOutput(new CommandLineInvocation(ExecutableLocation, SanitiseCommandLineArgs(arguments)));
        }

        static string[] SanitiseCommandLineArgs(IEnumerable<string> arguments) => arguments.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

        void ChmodExecutable()
        {
            if (isCustomExecutable && !CalamariEnvironment.IsRunningOnWindows)
            {
                //if this is a custom executable, chmod the exe before we run it
                ExecuteCommandAndLogOutput(new CommandLineInvocation("chmod",
                                                                     "+x",
                                                                     ExecutableLocation));
            }
        }

        CommandResult ExecuteCommandViaScript(IEnumerable<string> arguments)
        {
            var syntax = ScriptSyntaxHelper.GetPreferredScriptSyntaxForEnvironment();
            var command = BuildHelmScriptCommand(arguments, syntax);
            var fileName = SyntaxSpecificFileName(syntax);
            var scriptExecutor = GetScriptExecutor(syntax);
            
            using (new TemporaryFile(fileName))
            {
                fileSystem.OverwriteFile(fileName, command);
                
                return scriptExecutor.Execute(new Script(fileName), variables, commandLineRunner, environmentVars);
            }
        }

        string BuildHelmScriptCommand(IEnumerable<string> arguments, ScriptSyntax syntax)
        {
            var cmd = new List<string>();
            
            SetExecutable(cmd, syntax);
            cmd.AddRange(arguments.Where(s => !string.IsNullOrWhiteSpace(s)));

            var command = string.Join(" ", cmd);
            log.Verbose(command);
            return command;
        }
        
        void SetExecutable(List<string> commandBuilder, ScriptSyntax syntax)
        {
            if (isCustomExecutable)
            {
                commandBuilder.Add(syntax == ScriptSyntax.PowerShell ? ". " : $"chmod +x \"{ExecutableLocation}\";");
            }
            
            commandBuilder.Add(ExecutableLocation);
        }
        
        string SyntaxSpecificFileName(ScriptSyntax syntax)
        {
            return Path.Combine(workingDirectory, syntax == ScriptSyntax.PowerShell ? "Calamari.HelmUpgrade.ps1" : "Calamari.HelmUpgrade.sh");
        }
        
        IScriptExecutor GetScriptExecutor(ScriptSyntax syntax)
        {
            switch (syntax)
            {
                case ScriptSyntax.PowerShell:
                    return new PowerShellScriptExecutor();
                case ScriptSyntax.Bash:
                    return new BashScriptExecutor();
                default:
                    throw new NotSupportedException($"{syntax} script is not supported for Helm upgrade execution");
            }
        }
    }
}