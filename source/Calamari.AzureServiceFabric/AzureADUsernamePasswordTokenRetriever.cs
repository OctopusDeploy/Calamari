using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Fabric.Security;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.AzureServiceFabric
{
    internal class AzureADUsernamePasswordTokenRetriever
    {
        internal static string GetAccessToken(AzureActiveDirectoryMetadata aad, string aadUsername, string aadPassword, ILog log)
        {
            try
            {
                var app = PublicClientApplicationBuilder
                       .Create(aad.ClientApplication)
                       .WithAuthority(aad.Authority)
                       .Build();

                var scope = new[] { $"{aad.ClusterApplication}/.default" };
                var authResult = app.AcquireTokenByUsernamePassword(scope, aadUsername, aadPassword)
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                return authResult.AccessToken;
            }
            catch (MsalUiRequiredException ex)
            {
                log.Error($"Unable to retrieve authentication token: User interaction is required to connect to the Service Fabric cluster with the provided account. Please change the account's settings, or use a different account.");
                log.Error($"Details: {ex.PrettyPrint()}");
                return "BAD_TOKEN"; // You cannot return null or an empty value here or the Azure lib fails trying to call a lib that doesn't exist "System.Fabric.AzureActiveDirectory.Client" 
            }
            catch (Exception ex)
            {
                log.Error($"Connect failed: {ex.PrettyPrint()}");
                return "BAD_TOKEN";
            }
        }
    }
}
