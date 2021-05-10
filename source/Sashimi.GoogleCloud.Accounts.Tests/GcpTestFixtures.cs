using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Tests.Shared;
using FluentAssertions;
using Google.Apis.Auth.OAuth2;
using NUnit.Framework;
using Octopus.Data.Model;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;

namespace Sashimi.GoogleCloud.Accounts.Tests
{
    [TestFixture]
    public class GoogleCloudTestFixtures
    {
        [Test]
        public void Verify_ShouldUseTheHttpClientProvidedFromTheOctopusHttpClientFactory()
        {
            var jsonPath = @"D:\terraform-sample-312901-525251fa48e0.json";
            var credential = GoogleCredential.FromFile(jsonPath);
        }
        
        class TestHttpClientHandler : HttpClientHandler
        {
            public TestHttpClientHandler()
            {
                RequestLog = new List<HttpRequestMessage>();
            }

            public IList<HttpRequestMessage> RequestLog { get; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestLog.Add(request);

                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}