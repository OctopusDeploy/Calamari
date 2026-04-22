using System.Linq;
using System.Threading.Tasks;
using Calamari.Aws.Deployment.Conventions;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Ecs
{
    [TestFixture]
    public class SetEcsOutputVariablesConventionTests
    {
        class TestableConvention : SetEcsOutputVariablesConvention
        {
            readonly string serviceLogicalId;

            public TestableConvention(
                AwsEnvironmentGeneration environment,
                string stackName,
                string clusterName,
                string taskFamily,
                Calamari.Common.Plumbing.Logging.ILog log,
                string serviceLogicalId)
                : base(environment, stackName, clusterName, taskFamily, log)
            {
                this.serviceLogicalId = serviceLogicalId;
            }

            protected override Task<string> LookupServiceLogicalId() =>
                Task.FromResult(serviceLogicalId);
        }

        static AwsEnvironmentGeneration CreateEnvironment(string region)
        {
            var vars = new CalamariVariables();
            vars.Set("Octopus.Action.Aws.Region", region);
            var env = new AwsEnvironmentGeneration(new InMemoryLog(), vars);
            env.EnvironmentVars["AWS_REGION"] = region;
            return env;
        }

        [Test]
        public void Install_EmitsAllFiveOutputVariableServiceMessages()
        {
            var log = new InMemoryLog();
            var env = CreateEnvironment("us-east-1");
            var convention = new TestableConvention(env, "my-stack", "my-cluster", "my-family", log, "ServicemyService");
            var deployment = new RunningDeployment(new CalamariVariables());

            convention.Install(deployment);

            var setVarMessages = log.Messages.GetServiceMessagesOfType("setVariable");
            Assert.That(setVarMessages.Select(m => m.GetValue("name")), Is.EquivalentTo(new[]
            {
                "ServiceName", "ClusterName", "CloudFormationStackName", "TaskDefinitionFamily", "Region"
            }));
        }

        [Test]
        public void Install_SetsCorrectValues()
        {
            var log = new InMemoryLog();
            var env = CreateEnvironment("us-west-2");
            var convention = new TestableConvention(env, "my-stack", "my-cluster", "my-family", log, "ServicemyService");
            var deployment = new RunningDeployment(new CalamariVariables());

            convention.Install(deployment);

            var messages = log.Messages.GetServiceMessagesOfType("setVariable");

            Assert.That(messages.GetPropertyValue("ServiceName"), Is.EqualTo("ServicemyService"));
            Assert.That(messages.GetPropertyValue("ClusterName"), Is.EqualTo("my-cluster"));
            Assert.That(messages.GetPropertyValue("CloudFormationStackName"), Is.EqualTo("my-stack"));
            Assert.That(messages.GetPropertyValue("TaskDefinitionFamily"), Is.EqualTo("my-family"));
            Assert.That(messages.GetPropertyValue("Region"), Is.EqualTo("us-west-2"));
        }

        [Test]
        public void Install_ServiceLookupReturnsNull_SetsServiceNameToEmpty()
        {
            var log = new InMemoryLog();
            var env = CreateEnvironment("us-east-1");
            var convention = new TestableConvention(env, "my-stack", "my-cluster", "my-family", log, null);
            var deployment = new RunningDeployment(new CalamariVariables());

            convention.Install(deployment);

            var messages = log.Messages.GetServiceMessagesOfType("setVariable");

            Assert.That(messages.GetPropertyValue("ServiceName"), Is.EqualTo(""));
            Assert.That(messages.GetPropertyValue("ClusterName"), Is.EqualTo("my-cluster"));
        }

        [Test]
        public void Install_AlsoWritesOutputVariablesToDeploymentVariables()
        {
            var log = new InMemoryLog();
            var env = CreateEnvironment("us-east-1");
            var convention = new TestableConvention(env, "my-stack", "my-cluster", "my-family", log, "ServicemyService");
            var variables = new CalamariVariables();
            variables.Set("Octopus.Action.Name", "DeployEcs");
            var deployment = new RunningDeployment(variables);

            convention.Install(deployment);

            Assert.That(variables.Get("Octopus.Action[DeployEcs].Output.ServiceName"), Is.EqualTo("ServicemyService"));
            Assert.That(variables.Get("Octopus.Action[DeployEcs].Output.ClusterName"), Is.EqualTo("my-cluster"));
            Assert.That(variables.Get("Octopus.Action[DeployEcs].Output.Region"), Is.EqualTo("us-east-1"));
        }
    }
}
