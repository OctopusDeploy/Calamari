using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.ContextProviders;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes
{
    public class SetupKubectlAuthentication
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly ICommandLineRunner commandLineRunner;
        readonly IKubectl kubectl;
        private readonly ICalamariFileSystem fileSystem;
        readonly Dictionary<string, string> environmentVars;
        readonly string workingDirectory;

        public SetupKubectlAuthentication(IVariables variables,
            ILog log,
            ICommandLineRunner commandLineRunner,
            IKubectl kubectl,
            ICalamariFileSystem fileSystem,
            Dictionary<string, string> environmentVars,
            string workingDirectory)
        {
            this.variables = variables;
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.kubectl = kubectl;
            this.fileSystem = fileSystem;
            this.environmentVars = environmentVars;
            this.workingDirectory = workingDirectory;
        }

        public CommandResult Execute(string accountType)
        {
            foreach (var proxyVariable in GenerateProxyEnvironmentVariables())
            {
                environmentVars[proxyVariable.Key] = proxyVariable.Value;
            }

            // Make sure Kubectl exists and can be run
            if (!kubectl.TrySetKubectl())
            {
                return new CommandResult("Unable to setup Kubectl executable", 1);
            }

            var kubeConfig = CreateKubectlConfig();

            var @namespace = variables.Get(SpecialVariables.Namespace);
            if (string.IsNullOrEmpty(@namespace))
            {
                log.Verbose("No namespace provided. Using default");
                @namespace = "default";
            }

            if (!TrySetupContext(kubeConfig, @namespace, accountType))
            {
                return new CommandResult($"Unable to setup auth context for accountType {accountType}", 2);
            }

            if (!CreateNamespace(@namespace))
            {
                log.Verbose("Could not create namespace. Continuing on, as it may not be working directly with the target.");
            }

            var outputKubeConfig = variables.GetFlag(SpecialVariables.OutputKubeConfig);
            if (outputKubeConfig)
            {
                kubectl.ExecuteCommandAndAssertSuccess("config", "view");
            }

            return new CommandResult(string.Empty, 0);
        }

        bool TrySetupContext(string kubeConfig, string @namespace, string accountType)
        {
            var clusterUrl = variables.Get(SpecialVariables.ClusterUrl);
            var clientCert = variables.Get(SpecialVariables.ClientCertificate);
            var skipTlsVerification = variables.GetFlag(SpecialVariables.SkipTlsVerification) ? "true" : "false";

            if (new AzureContextProvider(variables, log, commandLineRunner, kubectl, environmentVars, workingDirectory)
                .TrySetContext(kubeConfig, @namespace, accountType, clusterUrl))
            {
                return true;
            }

            if (new GoogleCloudContextProvider(variables, log, commandLineRunner, kubectl, fileSystem, environmentVars, workingDirectory)
                .TrySetContext(@namespace, accountType, clusterUrl))
            {
                return true;
            }

            if (string.IsNullOrEmpty(clusterUrl))
            {
                log.Error("Kubernetes cluster URL is missing");
                return false;
            }

            if (new AwsContextProvider(variables, log, commandLineRunner, kubectl, environmentVars, workingDirectory)
                .TrySetContext(@namespace, accountType, clusterUrl, clientCert, skipTlsVerification))
            {
                return true;
            }

            if (new PodServiceAccountContextProvider(variables, log, kubectl, fileSystem)
                .TrySetContext(@namespace, accountType, clusterUrl, clientCert, skipTlsVerification))
            {
                return true;
            }

            if (new TokenContextProvider(variables, log, kubectl)
                .TrySetContext(@namespace, accountType, clusterUrl, clientCert, skipTlsVerification))
            {
                return true;
            }

            if (new UsernamePasswordContextProvider(variables, log, kubectl)
                .TrySetContext(@namespace, accountType, clusterUrl, clientCert, skipTlsVerification))
            {
                return true;
            }

            if (accountType == null && !string.IsNullOrEmpty(clientCert))
                return true;

            log.Error($"Account Type {accountType} is currently not valid for kubectl contexts");
            return false;
        }

        bool CreateNamespace(string @namespace)
        {
            if (TryExecuteCommandWithVerboseLoggingOnly("get", "namespace", @namespace))
                return true;

            return TryExecuteCommandWithVerboseLoggingOnly("create", "namespace", @namespace);
        }

        string CreateKubectlConfig()
        {
            var kubeConfig = Path.Combine(workingDirectory, "kubectl-octo.yml");

            // create an empty file, to suppress kubectl errors about the file missing
            fileSystem.WriteAllText(kubeConfig, string.Empty);

            environmentVars.Add("KUBECONFIG", kubeConfig);

            if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
            {
                ExecuteCommand("chmod", "u=rw,g=,o=", $"\"{kubeConfig}\"");
            }

            log.Verbose($"Temporary kubectl config set to {kubeConfig}");

            return kubeConfig;
        }

        void ExecuteCommand(string executable, params string[] arguments)
        {
            ExecuteCommand(new CommandLineInvocation(executable, arguments)).VerifySuccess();
        }

        bool TryExecuteCommandWithVerboseLoggingOnly(params string[] arguments)
        {
            return ExecuteCommandWithVerboseLoggingOnly(new CommandLineInvocation(kubectl.ExecutableLocation, arguments.Concat(new[] { "--request-timeout=1m" }).ToArray())).ExitCode == 0;
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

        /// <summary>
        /// This is a special case for when the invocation results in an error
        /// 1) but is to be expected as a valid scenario; and
        /// 2) we don't want to inform this at an error level when this happens.
        /// </summary>
        /// <param name="invocation"></param>
        /// <returns></returns>
        CommandResult ExecuteCommandWithVerboseLoggingOnly(CommandLineInvocation invocation)
        {
            invocation.EnvironmentVars = environmentVars;
            invocation.WorkingDirectory = workingDirectory;
            invocation.OutputAsVerbose = true;
            invocation.OutputToLog = false;

            var captureCommandOutput = new CaptureCommandOutput();
            invocation.AdditionalInvocationOutputSink = captureCommandOutput;

            var commandString = invocation.ToString();
            log.Verbose(commandString);

            var result = commandLineRunner.Execute(invocation);

            foreach (var message in captureCommandOutput.Messages)
            {
                log.Verbose(message.Text);
            }

            return result;
        }

        /// <remarks>
        /// Seam for testing.
        /// </remarks>
        protected virtual IEnumerable<EnvironmentVariable> GenerateProxyEnvironmentVariables()
        {
            return ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables();
        }
    }
}