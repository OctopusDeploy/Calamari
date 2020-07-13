using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.AWS
{
    [TestFixture]
    public class AwsEnvironmentGenerationFixture
    {
        [Test]
        [TestCase("arn:aws:iam::0123456789AB:role/test-role", "My session name", "900", 900)]
        [TestCase("arn:aws:iam::0123456789AB:role/test-role", "My session name", null, 0)]
        [TestCase("arn:aws:iam::0123456789AB:role/test-role", "My session name", "", 0)]
        public void CreatesAssumeRoleRequestWithExpectedParams(string arn, string sessionName, string duration, int? expectedDuration)
        {
            IVariables variables = new CalamariVariables();
            variables.Add("Octopus.Action.Aws.AssumeRole", "True");
            variables.Add("Octopus.Action.Aws.AssumedRoleArn", arn);
            variables.Add("Octopus.Action.Aws.AssumedRoleSession", sessionName);
            variables.Add("Octopus.Action.Aws.AssumeRoleSessionDurationSeconds", duration);

            var awsEnvironmentGenerator = new AwsEnvironmentGeneration(Substitute.For<ILog>(), variables);
            var request = awsEnvironmentGenerator.GetAssumeRoleRequest();
            request.RoleArn.Should().Be(arn);
            request.RoleSessionName.Should().Be(sessionName);
            request.DurationSeconds.Should().Be(expectedDuration);
        }
    }
}
