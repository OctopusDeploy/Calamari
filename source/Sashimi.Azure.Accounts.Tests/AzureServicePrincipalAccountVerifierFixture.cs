using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Tests.Shared;
using NSubstitute;
using NUnit.Framework;
using Octopus.Data.Model;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;

namespace Sashimi.Azure.Accounts.Tests
{
    [TestFixture]
    public class AzureServicePrincipalAccountVerifierFixture
    {
        static AzureServicePrincipalAccountVerifier GetAzureServicePrincipalAccountVerifier()
        {
            var httpMessageHandler = new TestHttpClientHandler();
            var clientFactory = Substitute.For<IOctopusHttpClientFactory>();
            clientFactory.HttpClientHandler.Returns(httpMessageHandler);

            return new AzureServicePrincipalAccountVerifier(new Lazy<IOctopusHttpClientFactory>(clientFactory));
        }

        [Test]
        public void Verify_ShouldSuccessWithValidCredential()
        {
            var accountDetails = new AzureServicePrincipalAccountDetails
            {
                SubscriptionNumber = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId),
                ClientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId),
                TenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId),
                Password = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword).ToSensitiveString()
            };

            var verifier = GetAzureServicePrincipalAccountVerifier();

            Assert.DoesNotThrowAsync(() => verifier.Verify(accountDetails, CancellationToken.None));
        }

        [Test]
        public void Verify_ShouldFailWithWrongCredential()
        {
            var accountDetails = new AzureServicePrincipalAccountDetails
            {
                SubscriptionNumber = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId),
                ClientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId),
                TenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId),
                Password = "InvalidPassword".ToSensitiveString()
            };

            var verifier = GetAzureServicePrincipalAccountVerifier();

            Assert.ThrowsAsync<Microsoft.IdentityModel.Clients.ActiveDirectory.AdalServiceException>(() => verifier.Verify(accountDetails, CancellationToken.None));
        }

        [Test]
        public void Verify_ShouldNotCacheClientCredentials()
        {
            var accountDetails = new AzureServicePrincipalAccountDetails
            {
                SubscriptionNumber = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId),
                ClientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId),
                TenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId),
                Password = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword).ToSensitiveString()
            };

            var verifier = GetAzureServicePrincipalAccountVerifier();
            verifier.Verify(accountDetails, CancellationToken.None);

            accountDetails.Password = "InvalidPassword".ToSensitiveString();
            Assert.ThrowsAsync<Microsoft.IdentityModel.Clients.ActiveDirectory.AdalServiceException>(() => verifier.Verify(accountDetails, CancellationToken.None));
        }
    }

    class TestHttpClientHandler : HttpClientHandler
    {
        public TestHttpClientHandler()
        {
            RequestLog = new List<HttpRequestMessage>();
        }

        IList<HttpRequestMessage> RequestLog { get; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestLog.Add(request);

            return base.SendAsync(request, cancellationToken);
        }
    }
}