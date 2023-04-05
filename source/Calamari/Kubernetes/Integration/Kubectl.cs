using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using Octopus.CoreUtilities;
using Octopus.Versioning.Semver;

namespace Calamari.Kubernetes.Integration
{
    public class Kubectl : CommandLineTool
    {
        readonly string customKubectlExecutable;

        public Kubectl(string customKubectlExecutable, ILog log, ICommandLineRunner commandLineRunner, string workingDirectory, Dictionary<string, string> environmentVars)
            : base(log, commandLineRunner, workingDirectory, environmentVars)
        {
            this.customKubectlExecutable = customKubectlExecutable;
        }

        public bool TrySetKubectl()
        {
            if (string.IsNullOrEmpty(customKubectlExecutable))
            {
                var foundExecutable = CalamariEnvironment.IsRunningOnWindows
                    ? base.ExecuteCommandAndReturnOutput("where", "kubectl.exe").FirstOrDefault()
                    : base.ExecuteCommandAndReturnOutput("which", "kubectl").FirstOrDefault();

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
                log.Verbose($"Found kubectl and successfully verified it can be executed.");
                return true;
            }

            log.Error($"Found kubectl at {ExecutableLocation}, but unable to successfully execute it. See https://g.octopushq.com/KubernetesTarget for more information.");
            return false;
        }

        bool TryExecuteKubectlCommand(params string[] arguments)
        {
            return ExecuteCommandAndLogOutput(new CommandLineInvocation(ExecutableLocation, arguments.Concat(new[] { "--request-timeout=1m" }).ToArray())).ExitCode == 0;
        }

        public CommandResult ExecuteCommand(params string[] arguments)
        {
            var kubectlArguments = arguments.Concat(new[] { "--request-timeout=1m" }).ToArray();
            var commandInvocation = new CommandLineInvocation(ExecutableLocation, kubectlArguments);
            return ExecuteCommandAndLogOutput(commandInvocation);
        }

        public void ExecuteCommandAndAssertSuccess(params string[] arguments)
        {
            var result = ExecuteCommand(arguments);
            result.VerifySuccess();
        }

        public IEnumerable<string> ExecuteCommandAndReturnOutput(params string[] arguments) =>
            base.ExecuteCommandAndReturnOutput(ExecutableLocation, arguments);

        public Maybe<SemanticVersion> GetVersion()
        {
            var kubectlVersionOutput = ExecuteCommandAndReturnOutput("version", "--client", "--output=json");
            var kubeCtlVersionJson = string.Join(" ", kubectlVersionOutput);
            try
            {
                var clientVersion = JsonConvert.DeserializeAnonymousType(kubeCtlVersionJson, new { clientVersion = new { gitVersion = "1.0.0" } });
                var kubectlVersionString = clientVersion?.clientVersion?.gitVersion?.TrimStart('v');
                if (kubectlVersionString != null)
                {
                    return Maybe<SemanticVersion>.Some(new SemanticVersion(kubectlVersionString));
                }
            }
            catch (Exception e)
            {
                log.Verbose($"Unable to determine kubectl version. Failed with error message: {e.Message}");
            }

            return Maybe<SemanticVersion>.None;
        }
    }
}
