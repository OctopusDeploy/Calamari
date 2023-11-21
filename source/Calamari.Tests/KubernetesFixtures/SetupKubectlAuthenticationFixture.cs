using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Integration;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class SetupKubectlAuthenticationFixture
    {
        private IVariables variables;
        private ILog log;
        private ICommandLineRunner commandLineRunner;
        private IKubectl kubectl;
        private Dictionary<string, string> environmentVariables;
        private string workingDirectory;

        [SetUp]
        public void Setup()
        {
            variables = new CalamariVariables();
            log = Substitute.For<ILog>();
            commandLineRunner = Substitute.For<ICommandLineRunner>();
            kubectl = Substitute.For<IKubectl>();
            kubectl.TrySetKubectl().Returns(true);
            environmentVariables = new Dictionary<string, string>();
            workingDirectory = "/working/directory";
        }

        public SetupKubectlAuthentication CreateSut() => new TestableSetupKubectlAuthentication(variables, log, commandLineRunner, kubectl, environmentVariables, workingDirectory);

        [Test]
        public void Execute_SkipsAuthentication_WhenDeploymentTargetIsKubernetesTentacle()
        {
            var sut = CreateSut();
            variables.Add(MachineVariables.DeploymentTargetType, SetupKubectlAuthentication.KubernetesTentacleTargetTypeId);

            var result = sut.Execute(null);

            kubectl.Received().TrySetKubectl();
            result.VerifySuccess();
            log.Received().Info(SetupKubectlAuthentication.SkippingAuthenticationMessage);
            environmentVariables.Should().NotContainKey("KUBECONFIG");
        }

        [TestCase("Kubernetes")]
        [TestCase("TentaclePassive")]
        [TestCase("TentacleActive")]
        [TestCase("RandomOtherThing")]
        public void Execute_DoesAuthentication_WhenDeploymentTargetIsNotKubernetesTentacle(string targetTypeId)
        {
            var sut = CreateSut();
            variables.Add(MachineVariables.DeploymentTargetType, targetTypeId);

            Action act = () => sut.Execute(null);

            act.Should().Throw<Exception>(because: "no authentication variables are setup so we expect this to fail.");
        }

        public class TestableSetupKubectlAuthentication : SetupKubectlAuthentication
        {
            public TestableSetupKubectlAuthentication(IVariables variables, ILog log, ICommandLineRunner commandLineRunner, IKubectl kubectl, Dictionary<string, string> environmentVars, string workingDirectory) : base(variables, log, commandLineRunner, kubectl, environmentVars, workingDirectory)
            {
            }

            public IEnumerable<EnvironmentVariable> ProxyEnvironmentVariables { get; set; }

            protected override IEnumerable<EnvironmentVariable> GenerateProxyEnvironmentVariables()
            {
                return ProxyEnvironmentVariables ?? Enumerable.Empty<EnvironmentVariable>();
            }
        }
    }
}