using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using NUnit.Framework.Internal;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

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

        [Test]
        public async Task UsesGenericContainerCredentialsWhenAvailable()
        {
            IVariables variables = new CalamariVariables();
            var server = WireMockServer.Start();
            
            // Generate data - Keeping as close as possible to AWS
            var authToken = Randomizer.CreateRandomizer().GetString(1120, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/");
            var accessKeyId = Randomizer.CreateRandomizer().GetString(20, "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
            var secretAccessKey = Randomizer.CreateRandomizer().GetString(40, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+");
            var sessionToken = Randomizer.CreateRandomizer().GetString(1120, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/");
            var accountId = Randomizer.CreateRandomizer().GetString(12, "0123456789");
            
            var jsonResponse = $@"
            {{
                ""AccessKeyId"": ""{accessKeyId}"",
                ""SecretAccessKey"": ""{secretAccessKey}"",
                ""Token"": ""{sessionToken}"",
                ""AccountId"": ""{accountId}"",
                ""Expiration"": ""{DateTime.UtcNow.AddHours(12):yyyy-MM-ddTHH:mm:ssZ}""
            }}";
            
            server
                .Given(
                       Request.Create()
                              // This is the normal path for EKS Pod Identity Server
                              .WithPath("/v1/credentials")
                              // Match the auth token provided to the pod credentials
                              .WithHeader("Authorization", authToken, MatchBehaviour.AcceptOnMatch)
                              .UsingGet()
                      )
                .RespondWith(
                             Response.Create()
                                     .WithStatusCode(200)
                                     .WithHeader("Content-Type", "application/json")
                                     .WithBody(jsonResponse)
                            );
            
            AwsEnvironmentGeneration awsEnvironmentGenerator;
            using (var tokenPath = new TemporaryFile(Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString())))
            {
                File.WriteAllText(tokenPath.FilePath, authToken);
                Environment.SetEnvironmentVariable("AWS_CONTAINER_AUTHORIZATION_TOKEN_FILE", tokenPath.FilePath);
                Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_FULL_URI", $"http://127.0.0.1:{server.Port}/v1/credentials");
                awsEnvironmentGenerator = await AwsEnvironmentGeneration.Create(Substitute.For<ILog>(), variables);
            }
            
            awsEnvironmentGenerator.EnvironmentVars.Should().Contain(new KeyValuePair<string, string>("AWS_ACCESS_KEY_ID", accessKeyId));
            awsEnvironmentGenerator.EnvironmentVars.Should().Contain(new KeyValuePair<string, string>("AWS_SECRET_ACCESS_KEY", secretAccessKey));
            awsEnvironmentGenerator.EnvironmentVars.Should().Contain(new KeyValuePair<string, string>("AWS_SESSION_TOKEN", sessionToken));
        }
    }
}
