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
    public class Kubectl : CommandLineTool, IKubectl
    {
        readonly string customKubectlExecutable;
        private bool isSet;
        List<string> defaultCommandArgs = new List<string> { "--request-timeout=1m" };

        public Kubectl(IVariables variables, ILog log, ICommandLineRunner commandLineRunner)
            : this(variables,
                   log,
                   commandLineRunner,
                   Environment.CurrentDirectory,
                   new Dictionary<string, string>())
        {
        }

        public Kubectl(IVariables variables,
                       ILog log,
                       ICommandLineRunner commandLineRunner,
                       string workingDirectory,
                       Dictionary<string, string> environmentVariables) : base(log, commandLineRunner, workingDirectory, environmentVariables)
        {
            customKubectlExecutable = variables.Get("Octopus.Action.Kubernetes.CustomKubectlExecutable");
        }

        public void SetWorkingDirectory(string directory)
        {
            workingDirectory = directory;
        }

        public void SetEnvironmentVariables(Dictionary<string, string> variables)
        {
            environmentVars = variables;
        }

        public void SetKubectl()
        {
            if (isSet)
            {
                return;
            }

            if (string.IsNullOrEmpty(customKubectlExecutable))
            {
                var result = CalamariEnvironment.IsRunningOnWindows
                    ? base.ExecuteCommandAndReturnOutput("where", "kubectl.exe")
                    : base.ExecuteCommandAndReturnOutput("which", "kubectl");

                var foundExecutable = result.Output.InfoLogs.FirstOrDefault();

                if (string.IsNullOrEmpty(foundExecutable))
                {
                    throw new KubectlException("Could not find kubectl. Make sure kubectl is on the PATH. See https://g.octopushq.com/KubernetesTarget for more information.");
                }

                ExecutableLocation = foundExecutable?.Trim();
            }
            else if (!File.Exists(customKubectlExecutable))
            {
                throw new KubectlException($"The custom kubectl location of {customKubectlExecutable} does not exist. See https://g.octopushq.com/KubernetesTarget for more information.");
            }
            else
            {
                ExecutableLocation = customKubectlExecutable;
            }

            if (!TryExecuteKubectlCommand("version", "--client", "--output=yaml"))
            {
                throw new KubectlException($"Found kubectl at {ExecutableLocation}, but unable to successfully execute it. See https://g.octopushq.com/KubernetesTarget for more information.");
            }

            log.Verbose($"Found kubectl and successfully verified it can be executed.");
            isSet = true;
        }

        bool TryExecuteKubectlCommand(params string[] arguments)
        {
            return ExecuteCommandAndLogOutput(new CommandLineInvocation(ExecutableLocation, arguments.Concat(defaultCommandArgs).ToArray())).ExitCode == 0;
        }

        public CommandResult ExecuteCommand(params string[] arguments)
        {
            var kubectlArguments = arguments.Concat(defaultCommandArgs).ToArray();
            var commandInvocation = new CommandLineInvocation(ExecutableLocation, kubectlArguments);
            return ExecuteCommandAndLogOutput(commandInvocation);
        }

        /// <summary>
        /// This is a special case for when the invocation results in an error
        /// 1) but is to be expected as a valid scenario; and
        /// 2) we don't want to inform this at an error level when this happens.
        /// </summary>
        public CommandResult ExecuteCommandWithVerboseLoggingOnly(params string[] arguments)
        {
            var kubectlArguments = arguments.Concat(defaultCommandArgs).ToArray();
            var commandInvocation = new CommandLineInvocation(ExecutableLocation, kubectlArguments);
            return ExecuteCommandAndLogOutputAsVerbose(commandInvocation);
        }

        public void ExecuteCommandAndAssertSuccess(params string[] arguments)
        {
            var result = ExecuteCommand(arguments);
            result.VerifySuccess();
        }

        public CommandResultWithOutput ExecuteCommandAndReturnOutput(params string[] arguments) =>
            base.ExecuteCommandAndReturnOutput(ExecutableLocation, arguments);

        public Maybe<SemanticVersion> GetVersion()
        {
            var kubectlVersionOutput = ExecuteCommandAndReturnOutput("version", "--client", "--output=json").Output.InfoLogs;
            var kubeCtlVersionJson = string.Join(" ", kubectlVersionOutput);
            try
            {
                var clientVersion = JsonConvert.DeserializeAnonymousType(kubeCtlVersionJson, new { clientVersion = new { gitVersion = "1.0.0" } });
                var kubectlVersionString = clientVersion?.clientVersion?.gitVersion;
                if (kubectlVersionString != null)
                {
                    return Maybe<SemanticVersion>.Some(SemVerFactory.CreateVersion(kubectlVersionString));
                }
            }
            catch (Exception e)
            {
                log.Verbose($"Unable to determine kubectl version. Failed with error message: {e.Message}");
            }

            return Maybe<SemanticVersion>.None;
        }

        public void DisableRequestTimeoutArgument()
        {
            var idx = defaultCommandArgs.FindIndex(x => x.Contains("request-timeout"));
            defaultCommandArgs.RemoveAt(idx);
        }
    }

    public interface IKubectl
    {
        void SetWorkingDirectory(string directory);
        void SetEnvironmentVariables(Dictionary<string, string> variables);
        void SetKubectl();
        CommandResult ExecuteCommand(params string[] arguments);
        CommandResult ExecuteCommandWithVerboseLoggingOnly(params string[] arguments);
        void ExecuteCommandAndAssertSuccess(params string[] arguments);
        CommandResultWithOutput ExecuteCommandAndReturnOutput(params string[] arguments);
        Maybe<SemanticVersion> GetVersion();
        string ExecutableLocation { get; }
        void DisableRequestTimeoutArgument();
    }
}