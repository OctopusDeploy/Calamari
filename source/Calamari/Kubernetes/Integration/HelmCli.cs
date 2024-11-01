using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.Integration
{
    public class HelmCli : CommandLineTool
    {
        public HelmCli(ILog log, ICommandLineRunner commandLineRunner, string workingDirectory, Dictionary<string, string> environmentVars)
            : base(log, commandLineRunner, workingDirectory, environmentVars)
        {
            ExecutableLocation = "helm";
        }

        readonly List<string> builtInArguments = new List<string>();

        public HelmCli WithExecutable(string customExecutable)
        {
            ExecutableLocation = customExecutable;
            return this;
        }

        public HelmCli WithExecutable(IVariables variables)
        {
            var helmExecutable = variables.Get(SpecialVariables.Helm.CustomHelmExecutable);
            if (string.IsNullOrWhiteSpace(helmExecutable))
            {
                return this;
            }

            if (variables.GetIndexes(PackageVariables.PackageCollection)
                         .Contains(SpecialVariables.Helm.Packages.CustomHelmExePackageKey)
                && !Path.IsPathRooted(helmExecutable))
            {
                var fullPath = Path.GetFullPath(Path.Combine(workingDirectory, SpecialVariables.Helm.Packages.CustomHelmExePackageKey, helmExecutable));
                log.Info($"Using custom helm executable at {helmExecutable} from inside package. Full path at {fullPath}");

                return WithExecutable(fullPath);
            }
            else
            {
                log.Info($"Using custom helm executable at {helmExecutable}");
                return WithExecutable(helmExecutable);
            }
        }

        public HelmCli WithNamespace(IVariables variables)
        {
            var @namespace = variables.Get(SpecialVariables.Helm.Namespace);
            if (!string.IsNullOrWhiteSpace(@namespace))
            {
                builtInArguments.Add($"--namespace {@namespace}");
            }

            return this;
        }

        public int? GetCurrentRevision(string releaseName)
        {
            var result = ExecuteCommandAndReturnOutput("get", "metadata", releaseName, "-o json");


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
            var result = ExecuteCommandAndReturnOutput("get", "manifest", releaseName, $"--revision {revisionNumber}");
            result.Result.VerifySuccess();

            return result.Output.MergeInfoLogs();
        }

        public CommandResultWithOutput ExecuteCommandAndReturnOutput(params string[] arguments) =>
            base.ExecuteCommandAndReturnOutput(ExecutableLocation, arguments.Concat(builtInArguments).ToArray());
    }
}