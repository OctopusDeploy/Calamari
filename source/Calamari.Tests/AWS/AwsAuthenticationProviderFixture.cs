using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS
{
   [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class AwsAuthenticationProviderFixture
    {
        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;
        IVariables variables;

        [SetUp]
        public void SetUp()
        {
            variables = new CalamariVariables();
            variables.Add(AuthenticationVariables.Aws.Region, RegionEndpoint.USWest2.SystemName);
        }

        [Test]
        public async Task GetEcrCredentials_ReturnsValidCredentials()
        {
            var accessKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, cancellationToken);
            var secretKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, cancellationToken);
            
            if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
            {
                Assert.Ignore("AWS credentials not available for testing");
                return;
            }

            var credentials = await AwsAuthenticationProvider.GetEcrAccessKeyCredentials(variables, accessKey, secretKey);

            credentials.Should().NotBeNull();
            credentials.Username.Should().Be("AWS");
            credentials.Password.Should().NotBeEmpty();
            credentials.RegistryUri.Should().Contain("amazonaws.com");
        }

        [Test]
        public void GetEcrCredentials_WithInvalidCredentials_ThrowsException()
        {
            var accessKey = "AKIAINVALID";
            var secretKey = "invalidSecretKey";

            Assert.ThrowsAsync<AuthenticationException>(async () => await AwsAuthenticationProvider.GetEcrAccessKeyCredentials(variables, accessKey, secretKey));
        }
    }
}