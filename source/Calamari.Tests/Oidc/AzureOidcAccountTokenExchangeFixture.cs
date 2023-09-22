using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace Calamari.Tests.Oidc
{
    public class Tests
    {
        const string TestAccessToken = "access-token-123";

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public async Task ShouldGetAccessToken()
        {
            var certLocation = "./Certificates/Windows/localhost.pfx";

            using (var server = WireMockServer.Start(new WireMockServerSettings
                   {
                       UseSSL = true,
                       CertificateSettings = new WireMockCertificateSettings
                       {
                           X509StoreLocation = certLocation
                       }
                   }))
            {
                server.Given(
                             Request.Create()
                                    .WithPath("/tenant-xxx/oauth2/v2.0/token")
                                    .UsingPost()
                                    .WithHeader(headers => headers["Content-Type"].Contains("application/x-www-form-urlencoded"))
                                    .WithBody(inputs => inputs?["scope"] == "https://management-url/.default")
                                    .WithBody(inputs => inputs?["client_id"] == "client-xxx")
                                    .WithBody(inputs => inputs?["grant_type"] == "client_credentials")
                            )
                      .RespondWith(
                                   Response.Create()
                                           .WithSuccess()
                                           .WithBodyAsJson(new
                                           {
                                               token_type = "Bearer",
                                               expires_in = 3599,
                                               access_token = TestAccessToken
                                           })
                                  );

                var account = new AzureOidcAccount(new CalamariVariables
                {
                    { AccountVariables.SubscriptionId, "1111-111111111111-11111111" },
                    { AccountVariables.ClientId, "client-xxx" },
                    { AccountVariables.TenantId, "tenant-xxx" },
                    { AccountVariables.Jwt, "test jwt" },
                    { AccountVariables.Environment, "fake env" },
                    { AccountVariables.ResourceManagementEndPoint, "https://management-url/.default" },
                    { AccountVariables.ActiveDirectoryEndPoint, server.Url },
                    // The discovery endpoint doesn't need to work. Just needs to resolve.
                    { AccountVariables.InstanceDiscoveryUri, $"{server.Url}/discovery" }
                });

                var token = await account.GetAuthorizationToken(CancellationToken.None);

                token.Should().Be(TestAccessToken);
            }
        }
    }
}