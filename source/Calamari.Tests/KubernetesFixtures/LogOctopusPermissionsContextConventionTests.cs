using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Conventions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class LogOctopusPermissionsContextConventionTests
    {
        [Test]
        public void PrettyPrintsValueFromEnvironmentVariable()
        {
            var logger = Substitute.For<ILog>();
            Environment.SetEnvironmentVariable(LogOctopusPermissionsContextConvention.OpcPermissionsContext, "{\"key\":\"value\",\"object\":{\"nestedKey\":\"nestedValue\"},\"array\":[1,2,3]}");
            const string expected = @"{
  ""key"": ""value"",
  ""object"": {
    ""nestedKey"": ""nestedValue""
  },
  ""array"": [
    1,
    2,
    3
  ]
}";

            var convention = new LogOctopusPermissionsContextConvention(logger);
            convention.Install(new RunningDeployment(new CalamariVariables()));

            logger.Received().Verbose(expected);
        }

        [Test]
        public void LogsNothingWithoutEnvironmentVariableValue()
        {
            var logger = Substitute.For<ILog>();
            Environment.SetEnvironmentVariable(LogOctopusPermissionsContextConvention.OpcPermissionsContext, "");

            var convention = new LogOctopusPermissionsContextConvention(logger);
            convention.Install(new RunningDeployment(new CalamariVariables()));

            logger.Received(0).Verbose(Arg.Any<string>());
        }

        [Test]
        public void LogsNothingWithoutEnvironmentVariableSet()
        {
            var logger = Substitute.For<ILog>();

            var convention = new LogOctopusPermissionsContextConvention(logger);
            convention.Install(new RunningDeployment(new CalamariVariables()));

            logger.Received(0).Verbose(Arg.Any<string>());
        }
    }
}