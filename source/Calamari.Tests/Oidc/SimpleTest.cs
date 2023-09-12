using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using FluentAssertions;
using NUnit.Framework;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Calamari.Tests.Oidc
{
    [TestFixture]
    public class SimpleTest
    {
        [Test]
        public async Task SanityTest()
        {
            using (var server = WireMockServer.Start(port: 8443, ssl: true))
            {
                server.Given(Request.Create().WithPath("/test").UsingGet())
                      .RespondWith(
                                   Response.Create()
                                           .WithStatusCode(200)
                                           .WithBody("ok")
                                  );

                using (var client = HttpClientFactory.Create())
                {
                    var response = await client.GetAsync($"{server.Url}/test");
                    var body = await response.Content.ReadAsStringAsync();
                    body.Should().Be("ok");
                }
            }
        }

        [Test]
        public async Task ShouldGetAccessToken()
        {
            using (var server = WireMockServer.Start(port: 443, ssl: true))
            {
                server.Given(
                             Request.Create()
                                    .WithPath("/tenant-xxx/oauth2/v2.0/token")
                                    .UsingPost()
                                    .WithHeader(headers => headers["Content-Type"].Contains("application/x-www-form-urlencoded"))
                                    .WithBody(inputs => inputs["scope"] == "https://management-url/.default")
                                    .WithBody(inputs => inputs["client_id"] == "client-xxx")
                                    .WithBody(inputs => inputs["grant_type"] == "client_credentials")
                            )
                      .RespondWith(
                                   Response.Create()
                                           .WithSuccess()
                                           .WithBodyAsJson(new
                                           {
                                               token_type = "Bearer",
                                               expires_in = 3599,
                                               access_token = "access-123"
                                           })
                                  );

                var serverHost = new Uri(server.Url).Host + $":{server.Port}";

                server.Given(Request.Create().WithPath("/discovery").UsingGet())
                      .RespondWith(
                                   Response.Create()
                                           .WithSuccess()
                                           .WithBodyAsJson(new Dictionary<string, object>
                                           {
                                               { "api-version", "1.1" },
                                               {
                                                   "metadata", new[]
                                                   {
                                                       new
                                                       {
                                                           preferred_network = serverHost,
                                                           preferred_cache = serverHost,
                                                           aliases = new[] { serverHost }
                                                       }
                                                   }
                                               }
                                           }));

                // using (var client = HttpClientFactory.Create())
                // {
                //     var response = await client.GetAsync($"{server.Url}/discovery");
                //     var body = await response.Content.ReadAsStringAsync();
                //     body.Should().Be("ok");
                // }

                var account = new AzureOidcAccount(
                                                   "1111-111111111111-11111111",
                                                   "client-xxx",
                                                   "tenant-xxx",
                                                   "this shouldn't be needed",
                                                   "fake-env",
                                                   "https://management-url/.default",
                                                   server.Url);

                var token = await account.GetAuthorizationToken(CancellationToken.None);

                token.Should().Be("access-123");
            }
        }
    }
}