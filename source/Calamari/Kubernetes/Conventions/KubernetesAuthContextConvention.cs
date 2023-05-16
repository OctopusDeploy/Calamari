using System;
using Calamari.Common.Aws;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;

namespace Calamari.Kubernetes.Conventions
{
    /// <summary>
    /// An Implementation of IInstallConvention which setups Kubectl Authentication Context
    /// </summary>
    public class KubernetesAuthContextConvention : IInstallConvention
    {
        private readonly ILog log;
        private readonly ICommandLineRunner commandLineRunner;
        private readonly Lazy<AwsEnvironmentVariablesFactory> awsEnvironmentVariablesFactory;

        public KubernetesAuthContextConvention(
            ILog log,
            ICommandLineRunner commandLineRunner,
            Lazy<AwsEnvironmentVariablesFactory> awsEnvironmentVariablesFactory)
        {
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.awsEnvironmentVariablesFactory = awsEnvironmentVariablesFactory;
        }

        public void Install(RunningDeployment deployment)
        {
            var setupKubectlAuthentication = new SetupKubectlAuthentication(deployment.Variables,
                log,
                awsEnvironmentVariablesFactory,
                ScriptSyntax.PowerShell,
                commandLineRunner,
                deployment.EnvironmentVariables,
                deployment.CurrentDirectory);

            var accountType = deployment.Variables.Get("Octopus.Account.AccountType");

            setupKubectlAuthentication.Execute(accountType);
        }
    }
}