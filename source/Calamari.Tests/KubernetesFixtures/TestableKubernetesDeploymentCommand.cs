using System;
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.Commands;
using Calamari.Kubernetes.Integration;

namespace Calamari.Tests.KubernetesFixtures
{
    [Command(Name, Description = "used for tests")]
    public class TestableKubernetesDeploymentCommand : KubernetesDeploymentCommandBase
    {
        public const string Name = "test-kubernetes-command";
        private readonly Kubectl kubectl;

        public TestableKubernetesDeploymentCommand(
            ILog log,
            IDeploymentJournalWriter deploymentJournalWriter,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            IExtractPackage extractPackage,
            ISubstituteInFiles substituteInFiles,
            IStructuredConfigVariablesService structuredConfigVariablesService,
            Kubectl kubectl)
            : base(log, deploymentJournalWriter, variables, fileSystem, extractPackage,
                substituteInFiles, structuredConfigVariablesService, kubectl)
        {
            this.kubectl = kubectl;
        }

        protected override IEnumerable<IInstallConvention> CommandSpecificInstallConventions()
        {
            yield return new TestKubectlAuthConvention(kubectl);
        }

        private class TestKubectlAuthConvention : IInstallConvention
        {
            private readonly Kubectl kubectl;

            public TestKubectlAuthConvention(Kubectl kubectl)
            {
                this.kubectl = kubectl;
            }

            public void Install(RunningDeployment deployment)
            {
                if (!kubectl.TrySetKubectl())
                {
                    throw new InvalidOperationException("Unable to set KubeCtl");
                }

                kubectl.ExecuteCommand("cluster-info");
            }
        }
    }
}

