// using System;
// using System.IO;
// using System.Linq;
// using System.Security.Cryptography.X509Certificates;
// using System.Threading;
// using System.Threading.Tasks;
// using Calamari.CloudAccounts;
// using Calamari.Common.Plumbing.Variables;
// using Calamari.Testing.Helpers;
// using FluentAssertions;
// using NUnit.Framework;
// using WireMock.RequestBuilders;
// using WireMock.ResponseBuilders;
// using WireMock.Server;
// using WireMock.Settings;
//
// namespace Calamari.Tests.Oidc
// {
//     [TestFixture]
//     [Category(TestCategory.CompatibleOS.OnlyWindows)]
//     public class AzureOidcAccountTokenExchangeFixture
//     {
//         const string TestAccessToken = "access-token-123";
//         
//         string CertLocation => Path.GetFullPath("Oidc/Certificates/Windows/localhost.pfx");
//
//         [OneTimeSetUp]
//         public void SetUp()
//         {
//             var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
//             try
//             {
//                 store.Open(OpenFlags.ReadWrite);
//                 store.Add(new X509Certificate2(CertLocation, "password"));
//             }
//             finally
//             {
//                 store.Close();
//             }
//         }
//
//         [Test]
//         public async Task ShouldGetAccessToken()
//         {
//             using (var server = WireMockServer.Start(new WireMockServerSettings
//                    {
//                        UseSSL = true,
//                        CertificateSettings = new WireMockCertificateSettings
//                        {
//                            X509StoreLocation = CertLocation
//                        }
//                    }))
//             {
//                 server.Given(
//                              Request.Create()
//                                     .WithPath("/tenant-xxx/oauth2/v2.0/token")
//                                     .UsingPost()
//                                     .WithHeader(headers => headers["Content-Type"].Contains("application/x-www-form-urlencoded"))
//                                     .WithBody(inputs => inputs?["scope"] == "https://management-url/.default")
//                                     .WithBody(inputs => inputs?["client_id"] == "client-xxx")
//                                     .WithBody(inputs => inputs?["grant_type"] == "client_credentials")
//                             )
//                       .RespondWith(
//                                    Response.Create()
//                                            .WithSuccess()
//                                            .WithBodyAsJson(new
//                                            {
//                                                token_type = "Bearer",
//                                                expires_in = 3599,
//                                                access_token = TestAccessToken
//                                            })
//                                   );
//
//                 var account = new AzureOidcAccount(new CalamariVariables
//                 {
//                     { AccountVariables.SubscriptionId, "1111-111111111111-11111111" },
//                     { AccountVariables.ClientId, "client-xxx" },
//                     { AccountVariables.TenantId, "tenant-xxx" },
//                     { AccountVariables.Jwt, "test jwt" },
//                     { AccountVariables.Environment, "fake env" },
//                     { AccountVariables.ResourceManagementEndPoint, "https://management-url/.default" },
//                     { AccountVariables.ActiveDirectoryEndPoint, server.Url },
//                     // The discovery endpoint doesn't need to work. Just needs to resolve.
//                     { AccountVariables.InstanceDiscoveryUri, $"{server.Url}/discovery" }
//                 });
//
//                 var token = await account.GetAuthorizationToken(CancellationToken.None);
//
//                 token.Should().Be(TestAccessToken);
//             }
//         }
//     }
// }