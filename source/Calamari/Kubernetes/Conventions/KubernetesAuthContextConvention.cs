using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.Conventions
{
    /// <summary>
    /// An Implementation of IInstallConvention which setups Kubectl Authentication Context
    /// </summary>
    public class KubernetesAuthContextConvention : IInstallConvention
    {
        private readonly ILog log;
        private readonly ICommandLineRunner commandLineRunner;
        private readonly Func<Kubectl> kubectlFactory;

        public KubernetesAuthContextConvention(ILog log, ICommandLineRunner commandLineRunner, Func<Kubectl> kubectlFactory)
        {
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.kubectlFactory = kubectlFactory;
        }

        public void Install(RunningDeployment deployment)
        {
            var setupKubectlAuthentication = new SetupKubectlAuthentication(deployment.Variables,
                log,
                ScriptSyntax.PowerShell,
                commandLineRunner,
                deployment.EnvironmentVariables,
                deployment.CurrentDirectory);

            var accountType = deployment.Variables.Get("Octopus.Account.AccountType");

            setupKubectlAuthentication.Execute(accountType, kubectlFactory());
        }
    }
}