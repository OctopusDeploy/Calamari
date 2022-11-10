using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Kubernetes.Integration
{
    class Kubectl : CommandLineTool
    {
        string customKubectlExecutable;
        readonly IVariables variables;

        public string ExecutableLocation { get; private set; }

        public Kubectl(IVariables variables, string customKubectlExecutable, ILog log, ICommandLineRunner commandLineRunner, string workingDirectory, Dictionary<string, string> environmentVars, Dictionary<string, string> redactMap)
            : base(log, commandLineRunner, workingDirectory, environmentVars, redactMap)
        {
            this.variables = variables;
            this.customKubectlExecutable = customKubectlExecutable;
        }

        public bool TrySetKubectl()
        {
            if (string.IsNullOrEmpty(customKubectlExecutable))
            {
                var foundExecutable = CalamariEnvironment.IsRunningOnWindows
                    ? ExecuteCommandAndReturnOutput("where", "kubectl.exe").FirstOrDefault()
                    : ExecuteCommandAndReturnOutput("which", "kubectl").FirstOrDefault();

                if (string.IsNullOrEmpty(foundExecutable))
                {
                    log.Error("Could not find kubectl. Make sure kubectl is on the PATH. See https://g.octopushq.com/KubernetesTarget for more information.");
                    return false;
                }

                ExecutableLocation = foundExecutable?.Trim();
            }
            else
            {
                if (!File.Exists(customKubectlExecutable))
                {
                    log.Error($"The custom kubectl location of {customKubectlExecutable} does not exist. See https://g.octopushq.com/KubernetesTarget for more information.");
                    return false;
                }

                ExecutableLocation = customKubectlExecutable;
            }

            if (TryExecuteKubectlCommand("version", "--client", "--short"))
            {
                log.Verbose($"Found kubectl at {ExecutableLocation} and successfully verified it can be executed.");
                return true;
            }

            log.Error($"Found kubectl at {ExecutableLocation}, but unable to successfully execute it. See https://g.octopushq.com/KubernetesTarget for more information.");
            return false;
        }

        bool TryExecuteKubectlCommand(params string[] arguments)
        {
            return ExecuteCommandAndLogOutput(new CommandLineInvocation(ExecutableLocation, arguments.Concat(new[] { "--request-timeout=1m" }).ToArray())).ExitCode == 0;
        }
    }
}
