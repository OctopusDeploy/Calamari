using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;

namespace Calamari.Commands
{
    [Obsolete("This command is used exclusively by the Kustomize step package. It should be removed together with \"KustomizeStepMigrationFeatureToggle\" once the step migration has settled.")]
    [Command("kubernetes-object-status")]
    public class KubernetesObjectStatusReporterCommand : Command<KubernetesObjectStatusReporterCommandInput>
    {
        private readonly ILog log;
        private readonly IVariables variables;
        private readonly IResourceStatusReportExecutor statusReportExecutor;
        private readonly Kubectl kubectl;

        public KubernetesObjectStatusReporterCommand(
            ILog log,
            IVariables variables,
            IResourceStatusReportExecutor statusReportExecutor,
            Kubectl kubectl)
        {
            this.log = log;
            this.variables = variables;
            this.statusReportExecutor = statusReportExecutor;
            this.kubectl = kubectl;
        }

        protected override void Execute(KubernetesObjectStatusReporterCommandInput inputs)
        {
            var runningDeployment = new RunningDeployment(variables);

            if (!inputs.Enabled)
            {
                log.Info("Kubernetes Object Status reporting has been skipped.");
                return;
            }

            try
            {
                log.Info("Starting Kubernetes Object Status reporting.");

                ConfigureKubectl(runningDeployment.CurrentDirectory);

                var manifestPath = variables.Get(SpecialVariables.KustomizeManifest);
                var defaultNamespace = variables.Get(SpecialVariables.Namespace, "default");
                // When the namespace on a target was set and then cleared, it's going to be "" instead of null
                if (string.IsNullOrEmpty(defaultNamespace))
                {
                    defaultNamespace = "default";
                }

                var resources =
                    KubernetesYaml.GetDefinedResources(new[] { File.ReadAllText(manifestPath) }, defaultNamespace);

                var statusResult = statusReportExecutor.Start(inputs.Timeout, inputs.WaitForJobs, resources)
                                                       .WaitForCompletionOrTimeout(CancellationToken.None)
                                                       .GetAwaiter()
                                                       .GetResult();
                if (!statusResult)
                {
                    throw new CommandException("Unable to complete Kubernetes Report Status.");
                }
            }
            catch (Exception ex)
            {
                throw new CommandException("Failed to complete Kubernetes Report Status.", ex);
            }
        }

        private void ConfigureKubectl(string workingDirectory)
        {
            var kubeConfig = variables.Get(SpecialVariables.KubeConfig);
            var environmentVars = new Dictionary<string, string>
            {
                ["KUBECONFIG"] = kubeConfig
            };

            AzureCli.SetConfigDirectoryEnvironmentVariable(environmentVars, workingDirectory);

            foreach (var proxyVariable in ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables())
            {
                environmentVars[proxyVariable.Key] = proxyVariable.Value;
            }

            kubectl.SetEnvironmentVariables(environmentVars);
        }
    }

    public class KubernetesObjectStatusReporterCommandInput
    {
        public bool WaitForJobs { get; set; }
        public int Timeout { get; set; }
        public bool Enabled { get; set; }
    }
}