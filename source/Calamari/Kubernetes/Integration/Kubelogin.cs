using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Kubernetes.Integration
{
    public class KubeLogin : CommandLineTool
    {
        public KubeLogin(ILog log, ICommandLineRunner commandLineRunner, string workingDirectory, Dictionary<string, string> environmentVars)
            : base(log, commandLineRunner, workingDirectory, environmentVars)
        {
        }

        public bool TrySetKubeLogin()
        {
            var result = CalamariEnvironment.IsRunningOnWindows
                ? ExecuteCommandAndReturnOutput("where", "kubelogin")
                : ExecuteCommandAndReturnOutput("which", "kubelogin");
            
            var foundExecutable = result.Output.InfoLogs.FirstOrDefault();
            if (string.IsNullOrEmpty(foundExecutable))
            {
                log.Warn("Could not find kubelogin. Make sure kubelogin is on the PATH.");
                IsConfigured = false;
                return false;
            }

            ExecutableLocation = foundExecutable.Trim();
            IsConfigured = true;
            return true;
        }

        public bool IsConfigured { get; set; }

        public void ConfigureAksKubeLogin(string kubeConfigPath)
        {
            var arguments = new List<string>(new[]
            {
                "convert-kubeconfig",
                "-l",
                "azurecli",
                "--kubeconfig",
                $"\"{kubeConfigPath}\"",
            });

            ExecuteCommandAndAssertSuccess(arguments.ToArray());
        }

        public CommandResult ExecuteCommand(params string[] arguments)
        {
            var commandInvocation = new CommandLineInvocation(ExecutableLocation, arguments);
            return ExecuteCommandAndLogOutput(commandInvocation);
        }
        
        void ExecuteCommandAndAssertSuccess(params string[] arguments)
        {
            var result = ExecuteCommand(arguments);
            result.VerifySuccess();
        }
    }
}
