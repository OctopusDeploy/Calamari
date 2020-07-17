using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
﻿using Calamari.Common.Plumbing.Logging;

namespace Calamari.AzureWebApp
{
    static class ServicePrincipal
    {
        public static async Task<string> GetAuthorizationToken(string tenantId, string applicationId, string password, string managementEndPoint, string activeDirectoryEndPoint)
        {
            var authContext = GetContextUri(activeDirectoryEndPoint, tenantId);
            Log.Verbose($"Authentication Context: {authContext}");
            var context = new AuthenticationContext(authContext);
            var result = await context.AcquireTokenAsync(managementEndPoint, new ClientCredential(applicationId, password));
            return result.AccessToken;
        }

        static string GetContextUri(string activeDirectoryEndPoint, string tenantId)
        {
            if (!activeDirectoryEndPoint.EndsWith("/"))
            {
                return $"{activeDirectoryEndPoint}/{tenantId}";
            }
            return $"{activeDirectoryEndPoint}{tenantId}";
        }
    }
}