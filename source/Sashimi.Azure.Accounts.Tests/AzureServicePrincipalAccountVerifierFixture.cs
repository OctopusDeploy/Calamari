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

            Assert.DoesNotThrow(() => verifier.Verify(accountDetails));
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

            Assert.That(() => verifier.Verify(accountDetails), Throws.TypeOf<Microsoft.IdentityModel.Clients.ActiveDirectory.AdalServiceException>());
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
            verifier.Verify(accountDetails);

            accountDetails.Password = "InvalidPassword".ToSensitiveString();
            Assert.That(() => verifier.Verify(accountDetails), Throws.TypeOf<Microsoft.IdentityModel.Clients.ActiveDirectory.AdalServiceException>());
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