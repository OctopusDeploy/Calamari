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
    class Kubectl
    {
        readonly IVariables variables;
        string kubectl;
        readonly ILog log;
        readonly ICommandLineRunner commandLineRunner;
        readonly string workingDirectory;
        readonly Dictionary<string, string> environmentVars;
        Dictionary<string, string> redactMap;

        public string ExecutableLocation => kubectl;

        public Kubectl(IVariables variables, string kubectl, ILog log, ICommandLineRunner commandLineRunner, string workingDirectory, Dictionary<string, string> environmentVars, Dictionary<string, string> redactMap)
        {
            this.variables = variables;
            this.kubectl = kubectl;
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.workingDirectory = workingDirectory;
            this.environmentVars = environmentVars;
            // TODO: this should be set per-command-invocation, rather than in this constructor. It's not yet important, but will be for later commands to ensure we don't leak generated secrets in the logs.
            this.redactMap = redactMap;
        }

        public bool TrySetKubectl()
        {
            kubectl = variables.Get("Octopus.Action.Kubernetes.CustomKubectlExecutable");
            if (string.IsNullOrEmpty(kubectl))
            {
                kubectl = CalamariEnvironment.IsRunningOnWindows
                    ? ExecuteCommandAndReturnOutput("where", "kubectl.exe").FirstOrDefault()
                    : ExecuteCommandAndReturnOutput("which", "kubectl").FirstOrDefault();

                if (string.IsNullOrEmpty(kubectl))
                {
                    log.Error("Could not find kubectl. Make sure kubectl is on the PATH. See https://g.octopushq.com/KubernetesTarget for more information.");
                    return false;
                }

                kubectl = kubectl.Trim();
            }
            else if (!File.Exists(kubectl))
            {
                log.Error($"The custom kubectl location of {kubectl} does not exist. See https://g.octopushq.com/KubernetesTarget for more information.");
                return false;
            }

            if (TryExecuteKubectlCommand("version", "--client", "--short"))
            {
                return true;
            }

            log.Error($"Could not find kubectl. Make sure {kubectl} is on the PATH. See https://g.octopushq.com/KubernetesTarget for more information.");
            return false;
        }

        IEnumerable<string> ExecuteCommandAndReturnOutput(string exe, params string[] arguments)
        {
            var captureCommandOutput = new CaptureCommandOutput();
            var invocation = new CommandLineInvocation(exe, arguments)
            {
                EnvironmentVars = environmentVars,
                WorkingDirectory = workingDirectory,
                OutputAsVerbose = false,
                OutputToLog = false,
                AdditionalInvocationOutputSink = captureCommandOutput
            };

            var result = commandLineRunner.Execute(invocation);

            return result.ExitCode == 0
                ? captureCommandOutput.Messages.Where(m => m.Level == Level.Info).Select(m => m.Text).ToArray()
                : Enumerable.Empty<string>();
        }

        bool TryExecuteKubectlCommand(params string[] arguments)
        {
            return ExecuteCommand(new CommandLineInvocation(kubectl, arguments.Concat(new[] { "--request-timeout=1m" }).ToArray())).ExitCode == 0;
        }

        CommandResult ExecuteCommand(CommandLineInvocation invocation)
        {
            invocation.EnvironmentVars = environmentVars;
            invocation.WorkingDirectory = workingDirectory;
            invocation.OutputAsVerbose = false;
            invocation.OutputToLog = false;

            var captureCommandOutput = new CaptureCommandOutput();
            invocation.AdditionalInvocationOutputSink = captureCommandOutput;

            var commandString = invocation.ToString();
            commandString = redactMap.Aggregate(commandString, (current, pair) => current.Replace(pair.Key, pair.Value));
            log.Verbose(commandString);

            var result = commandLineRunner.Execute(invocation);

            foreach (var message in captureCommandOutput.Messages)
            {
                if (result.ExitCode == 0)
                {
                    log.Verbose(message.Text);
                    continue;
                }

                switch (message.Level)
                {
                    case Level.Info:
                        log.Verbose(message.Text);
                        break;
                    case Level.Error:
                        log.Error(message.Text);
                        break;
                }
            }

            return result;
        }
    }
}