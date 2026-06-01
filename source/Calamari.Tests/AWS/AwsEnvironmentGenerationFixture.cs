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
        [TestCase("arn:aws:iam::0123456789AB:role/test-role", "My session name", null, null)]
        [TestCase("arn:aws:iam::0123456789AB:role/test-role", "My session name", "", null)]
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
            variables.Add("Octopus.Action.Aws.Region", "us-east-1");
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
        [Test]
        public void FallsBackToAwsGlobalWhenRegionIsMissing()
        {
            IVariables variables = new CalamariVariables();
            variables.Add("Octopus.Account.AccountType", "AmazonWebServicesAccount");
            variables.Add("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Add("AWSAccount.AccessKey", "AKIAIOSFODNN7EXAMPLE");
            variables.Add("AWSAccount.SecretKey", "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY");

            var envGeneration = new AwsEnvironmentGeneration(Substitute.For<ILog>(), variables);
            envGeneration.AwsRegion.SystemName.Should().Be("aws-global");
        }

        // Repros the Terraform-step-on-EKS scenario from Issues #8337
        // worker Pod has IRSA env vars set, step is configured with UseInstanceRole=true and no explicit account.
        // PopulateKeysFromWebRole should win the fallback chain and populate session credentials from STS.
        [Test]
        public async Task UsesWebIdentityFromEksPodWhenInstanceRoleSelected()
        {
            IVariables variables = new CalamariVariables();
            variables.Add("Octopus.Action.AwsAccount.UseInstanceRole", "True");
            variables.Add("Octopus.Action.Aws.Region", "us-east-1");

            var roleArn = "arn:aws:iam::123456789012:role/eks-octopus-worker";
            var accessKeyId = Randomizer.CreateRandomizer().GetString(20, "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
            var secretAccessKey = Randomizer.CreateRandomizer().GetString(40, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+");
            var sessionToken = Randomizer.CreateRandomizer().GetString(1120, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/");

            var stsResponse = $@"<?xml version=""1.0""?>
<AssumeRoleWithWebIdentityResponse xmlns=""https://sts.amazonaws.com/doc/2011-06-15/"">
  <AssumeRoleWithWebIdentityResult>
    <Credentials>
      <AccessKeyId>{accessKeyId}</AccessKeyId>
      <SecretAccessKey>{secretAccessKey}</SecretAccessKey>
      <SessionToken>{sessionToken}</SessionToken>
      <Expiration>{DateTime.UtcNow.AddHours(1):yyyy-MM-ddTHH:mm:ssZ}</Expiration>
    </Credentials>
    <SubjectFromWebIdentityToken>system:serviceaccount:default:octopus-worker</SubjectFromWebIdentityToken>
    <AssumedRoleUser>
      <Arn>{roleArn}/octopus-aws-sdk-session</Arn>
      <AssumedRoleId>AROAEXAMPLE:octopus-aws-sdk-session</AssumedRoleId>
    </AssumedRoleUser>
  </AssumeRoleWithWebIdentityResult>
  <ResponseMetadata>
    <RequestId>00000000-0000-0000-0000-000000000000</RequestId>
  </ResponseMetadata>
</AssumeRoleWithWebIdentityResponse>";

            using var server = WireMockServer.Start();
            server
                .Given(Request.Create()
                              .UsingPost()
                              .WithBody(new RegexMatcher("Action=AssumeRoleWithWebIdentity")))
                .RespondWith(Response.Create()
                                     .WithStatusCode(200)
                                     .WithHeader("Content-Type", "text/xml")
                                     .WithBody(stsResponse));

            // Snapshot env so we don't bleed into subsequent tests (the existing
            // UsesGenericContainerCredentialsWhenAvailable test leaves CONTAINER_* set;
            // we must clear those or PopulateKeysFromContainerIdentity wins the fallback chain).
            var preserved = new Dictionary<string, string>
            {
                ["AWS_ROLE_ARN"] = Environment.GetEnvironmentVariable("AWS_ROLE_ARN"),
                ["AWS_WEB_IDENTITY_TOKEN_FILE"] = Environment.GetEnvironmentVariable("AWS_WEB_IDENTITY_TOKEN_FILE"),
                ["AWS_ENDPOINT_URL_STS"] = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL_STS"),
                ["AWS_CONTAINER_AUTHORIZATION_TOKEN_FILE"] = Environment.GetEnvironmentVariable("AWS_CONTAINER_AUTHORIZATION_TOKEN_FILE"),
                ["AWS_CONTAINER_CREDENTIALS_FULL_URI"] = Environment.GetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_FULL_URI"),
            };

            AwsEnvironmentGeneration awsEnvironmentGenerator;
            try
            {
                Environment.SetEnvironmentVariable("AWS_CONTAINER_AUTHORIZATION_TOKEN_FILE", null);
                Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_FULL_URI", null);

                using var tokenPath = new TemporaryFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
                File.WriteAllText(tokenPath.FilePath, "eyJhbGciOiJSUzI1NiIsImtpZCI6ImV4YW1wbGUifQ.fake-irsa-jwt.signature");

                Environment.SetEnvironmentVariable("AWS_ROLE_ARN", roleArn);
                Environment.SetEnvironmentVariable("AWS_WEB_IDENTITY_TOKEN_FILE", tokenPath.FilePath);
                Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL_STS", $"http://127.0.0.1:{server.Port}/");

                awsEnvironmentGenerator = await AwsEnvironmentGeneration.Create(Substitute.For<ILog>(), variables);
            }
            finally
            {
                foreach (var kvp in preserved)
                    Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }

            awsEnvironmentGenerator.EnvironmentVars.Should().Contain(new KeyValuePair<string, string>("AWS_ACCESS_KEY_ID", accessKeyId));
            awsEnvironmentGenerator.EnvironmentVars.Should().Contain(new KeyValuePair<string, string>("AWS_SECRET_ACCESS_KEY", secretAccessKey));
            awsEnvironmentGenerator.EnvironmentVars.Should().Contain(new KeyValuePair<string, string>("AWS_SESSION_TOKEN", sessionToken));
        }

        // Regression test for Issues #8337: when the step has no Octopus.Action.Aws.Region,
        // PopulateCommonSettings used to do EnvironmentVars["AWS_REGION"] = null. SilentProcessRunner
        // then overlays the dict onto ProcessStartInfo.EnvironmentVariables — and in .NET, setting
        // a dict value to null means 'remove the var' from the child env. That wiped the EKS Pod's
        // inherited AWS_REGION in the spawned terraform process, which errored with 'invalid AWS Region:'.
        //
        // Verified end-to-end on a real EKS+IRSA Pod (Calamari 2026.2.x): Test 5 in that harness
        // (Calamari-style env with no region) failed with that exact error; same harness's Test 4
        // (region set) succeeded. The fix is to skip the assignment when region is null/empty so the
        // parent env's value passes through unchanged.
        [Test]
        public async Task DoesNotOverwriteRegionWhenStepHasNoRegionConfigured()
        {
            IVariables variables = new CalamariVariables();
            variables.Add("Octopus.Account.AccountType", "AmazonWebServicesAccount");
            variables.Add("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Add("AWSAccount.AccessKey", "AKIAIOSFODNN7EXAMPLE");
            variables.Add("AWSAccount.SecretKey", "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY");
            // Deliberately NOT setting Octopus.Action.Aws.Region.

            // Stub verifyLogin so TryPopulateKeysDirectly succeeds without hitting STS.
            Func<Task<bool>> verifyLoginStub = () => Task.FromResult(true);
            var gen = await AwsEnvironmentGeneration.Create(Substitute.For<ILog>(), variables, verifyLoginStub);

            // Both keys must either be absent OR have a non-empty value. They must NOT exist with
            // null/empty values, because that's what SilentProcessRunner treats as 'remove the var'.
            foreach (var key in new[] { "AWS_REGION", "AWS_DEFAULT_REGION" })
            {
                if (gen.EnvironmentVars.TryGetValue(key, out var value))
                {
                    value.Should().NotBeNullOrWhiteSpace(
                        $"{key} must not be overlaid with null/empty when the step has no region — " +
                        "SilentProcessRunner treats a null dict value as 'remove the var' and wipes the " +
                        "parent process's region inherited from the EKS Pod, breaking the Terraform AWS provider");
                }
            }
        }
    }
}
