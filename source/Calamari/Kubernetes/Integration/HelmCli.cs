using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

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

        public HelmCli WithNamespace(string @namespace)
        {
            builtInArguments.Add($"--namespace {@namespace}");
            return this;
        }

        public string GetManifest(string releaseName)
        {
            var result = ExecuteCommandAndReturnOutput("get", "manifest", releaseName);
            result.Result.VerifySuccess();

            return result.Output.MergeInfoLogs();
        }

        public CommandResultWithOutput ExecuteCommandAndReturnOutput(params string[] arguments) =>
            base.ExecuteCommandAndReturnOutput(ExecutableLocation, builtInArguments.Concat(arguments).ToArray());
    }
}