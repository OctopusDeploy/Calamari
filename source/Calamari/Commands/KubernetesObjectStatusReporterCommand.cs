#if !NET40
using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;

namespace Calamari.Commands
{
    [Command("kubernetes-object-status")]
    public class KubernetesObjectStatusReporterCommand: Command<KubernetesObjectStatusReporterCommandInput>
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
            if (!inputs.Enabled)
            {
                log.Info("Kubernetes Object Status reporting has been skipped.");
                return;
            }
            
            try
            {
                log.Info("Starting Kubernetes Object Status reporting.");
                
                ConfigureKubectl();

                var manifestPath = variables.Get("Octopus.Kustomize.Manifest.Path");
                var defaultNamespace = variables.Get(SpecialVariables.Namespace, "default");
                // When the namespace on a target was set and then cleared, it's going to be "" instead of null
                if (string.IsNullOrEmpty(defaultNamespace))
                {
                    defaultNamespace = "default";
                }

                var resources =
                    KubernetesYaml.GetDefinedResources(new[] {File.ReadAllText(manifestPath)}, defaultNamespace);
                
                var statusResult = statusReportExecutor.Start(inputs.Timeout, inputs.WaitForJobs, resources).WaitForCompletionOrTimeout()
                    .GetAwaiter().GetResult();
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

        private void ConfigureKubectl()
        {
            var kubeConfig = variables.Get("Octopus.KubeConfig.Path");
            var environmentVars = new Dictionary<string, string> {{"KUBECONFIG", kubeConfig}};
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
#endif