#if !NET40
using System;
using System.Collections.Generic;
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

        private bool IsEnabled()
        {
            var resourceStatusEnabled = variables.GetFlag(SpecialVariables.ResourceStatusCheck);
            var isBlueGreen = variables.Get(SpecialVariables.DeploymentStyle) == "bluegreen";
            var isWaitDeployment = variables.Get(SpecialVariables.DeploymentWait) == "wait";
            if (!resourceStatusEnabled || isBlueGreen || isWaitDeployment)
            {
                return false;
            }

            var hasClusterUrl = !string.IsNullOrEmpty(variables.Get(SpecialVariables.ClusterUrl));
            var hasClusterName = !string.IsNullOrEmpty(variables.Get(SpecialVariables.AksClusterName)) ||
                                 !string.IsNullOrEmpty(variables.Get(SpecialVariables.EksClusterName)) ||
                                 !string.IsNullOrEmpty(variables.Get(SpecialVariables.GkeClusterName));
            return hasClusterUrl || hasClusterName;
        }
        
        protected override void Execute(KubernetesObjectStatusReporterCommandInput inputs)
        {
            if (!IsEnabled())
            {
                log.Info("Kubernetes Object Status reporting has been skipped.");
                return;
            }
            
            try
            {
                log.Info("Starting Kubernetes Object Status reporting.");
                
                ConfigureKubectl();

                var manifestPath = variables.Get("Octopus.Manifest.Path");
                var resources = KubernetesYaml.GetDefinedResources(new[] {manifestPath}, "default");
                var statusResult = statusReportExecutor.Start(resources).WaitForCompletionOrTimeout()
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
    
    public class KubernetesObjectStatusReporterCommandInput { }
}
#endif