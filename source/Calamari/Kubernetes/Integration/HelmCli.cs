using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.Integration
{
    public class HelmCli : CommandLineTool
    {
        readonly IVariables variables;
        bool isCustomExecutable;

        public HelmCli(ILog log, ICommandLineRunner commandLineRunner, RunningDeployment runningDeployment)
            : base(log, commandLineRunner, runningDeployment.CurrentDirectory, runningDeployment.EnvironmentVariables)
        {
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
    }
}