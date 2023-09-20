using Calamari.AzureAppService.Behaviors;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    public class TargetDiscoveryBehaviourUnitTestFixture
    {
        [Test]
        public async Task Execute_LogsError_WhenContextIsMissing()
        {
            // Arrange
            var variables = new CalamariVariables();
            var context = new RunningDeployment(variables);
            var log = new InMemoryLog();
            var sut = new TargetDiscoveryBehaviour(log);

            // Act
            await sut.Execute(context);

            // Assert
            log.StandardOut.Should().Contain(line => line.Contains("Could not find target discovery context in variable"));
            log.StandardOut.Should().Contain(line => line.Contains("Aborting target discovery."));
        }

        [Test]
        public async Task Exectute_LogsError_WhenContextIsInIncorrectFormat()
        {
            // Arrange
            var variables = new CalamariVariables();
            var context = new RunningDeployment(variables);
            context.Variables.Add("Octopus.TargetDiscovery.Context", @"{ authentication: { authenticationMethod: ""ServicePrincipal""}}");
            var log = new InMemoryLog();
            var sut = new TargetDiscoveryBehaviour(log);

            // Act
            await sut.Execute(context);

            // Assert
            log.StandardOut.Should().Contain(line => line.Contains("Target discovery context from variable Octopus.TargetDiscovery.Context is in wrong format"));
            log.StandardOut.Should().Contain(line => line.Contains("Aborting target discovery."));
        }

        [Test]
        public async Task Exectute_LogsError_WhenContextIsInIncorrectFormat_AuthenticationMethod()
        {
            // Arrange
            var variables = new CalamariVariables();
            var context = new RunningDeployment(variables);
            context.Variables.Add("Octopus.TargetDiscovery.Context", "bogus json");
            var log = new InMemoryLog();
            var sut = new TargetDiscoveryBehaviour(log);

            // Act
            await sut.Execute(context);

            // Assert
            log.StandardOut.Should().Contain(line => line.Contains("Could not read authentication method from target discovery context, Octopus.TargetDiscovery.Context is in wrong format: Unexpected character encountered while parsing value: b. Path '', line 0, position 0."));
            log.StandardOut.Should().Contain(line => line.Contains("Aborting target discovery."));
        }

        private void CreateVariables(RunningDeployment context, string targetDiscoveryContextJson)
        {
            context.Variables.Add("Octopus.TargetDiscovery.Context", targetDiscoveryContextJson);
        }
    }
}