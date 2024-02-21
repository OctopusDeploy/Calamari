#if !NET40
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
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
        readonly ILog log;
        readonly ICommandLineRunner commandLineRunner;
        readonly Kubectl kubectl;
        readonly ICalamariFileSystem fileSystem;

        public KubernetesAuthContextConvention(ILog log, ICommandLineRunner commandLineRunner, Kubectl kubectl, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.kubectl = kubectl;
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            var setupKubectlAuthentication = new SetupKubectlAuthentication(deployment.Variables,
                log,
                commandLineRunner,
                kubectl,
                fileSystem,
                deployment.EnvironmentVariables,
                deployment.CurrentDirectory);

            setupKubectlAuthentication.Execute();
        }
    }
}
#endif