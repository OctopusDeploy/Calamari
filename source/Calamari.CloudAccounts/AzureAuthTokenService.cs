using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Microsoft.Identity.Client;
using Microsoft.Rest;

namespace Calamari.CloudAccounts
{
    public interface IAzureAuthTokenService
    {
        Task<ServiceClientCredentials> GetCredentials(IAzureAccount account, CancellationToken cancellationToken);
        Task<string> GetAuthorizationToken(IAzureAccount account, CancellationToken cancellationToken);
    }

    public class AzureAuthTokenService : IAzureAuthTokenService
    {
        public async Task<ServiceClientCredentials> GetCredentials(IAzureAccount account, CancellationToken cancellationToken)
        {
            return new TokenCredentials(await GetAuthorizationToken(account, cancellationToken));
        }

        public async Task<string> GetAuthorizationToken(IAzureAccount account, CancellationToken cancellationToken)
        {
            switch (account.AccountType)
            {
                case AccountType.AzureServicePrincipal:
                    return await GetAuthorizationTokenForOidcAccount(account as AzureOidcAccount, cancellationToken);
                case AccountType.AzureOidc:
                    return await GetAuthorizationTokenForServicePrincipalAccount(account as AzureServicePrincipalAccount, cancellationToken);
                default:
                    throw new ArgumentException($"{account.AccountType} is not supported");
            }
        }
        
        async Task<string> GetAuthorizationTokenForOidcAccount(AzureOidcAccount account, CancellationToken cancellationToken)
        {
            var (tenantId, applicationId, token, managementEndPoint, activeDirectoryEndPoint, aureEnvironment) = (
                account.TenantId,
                account.ClientId,
                account.GetCredentials,
                account.ResourceManagementEndpointBaseUri,
                account.ActiveDirectoryEndpointBaseUri,
                account.AzureEnvironment);

            var authContext = GetOidcContextUri(string.IsNullOrEmpty(activeDirectoryEndPoint) ? "https://login.microsoftonline.com/" : activeDirectoryEndPoint, tenantId);
            Log.Verbose($"Authentication Context: {authContext}");

            var app = ConfidentialClientApplicationBuilder
                          .Create(applicationId)
                          .WithClientAssertion(token)
                          .WithAuthority(authContext)
                          .Build();

            // Default values set on a per cloud basis on AzureOidcAccount, if managementEndPoint is set on the account /.default is required.
            var scope = managementEndPoint == DefaultVariables.ResourceManagementEndpoint || string.IsNullOrEmpty(managementEndPoint)
                ? AzureOidcAccount.GetDefaultScope(aureEnvironment)
                : managementEndPoint.EndsWith(".default")
                    ? managementEndPoint
                    : $"{managementEndPoint}/.default";

            var result = await app.AcquireTokenForClient(new[] { scope })
                                  .WithTenantId(tenantId)
                                  .ExecuteAsync(cancellationToken)
                                  .ConfigureAwait(false);

            return result.AccessToken;
        }

        async Task<string> GetAuthorizationTokenForServicePrincipalAccount(AzureServicePrincipalAccount account, CancellationToken cancellationToken)
        { 
            var (tenantId, applicationId, password, managementEndPoint, activeDirectoryEndPoint) = (
                account.TenantId, 
                account.ClientId, 
                account.GetCredentials,
                account.ResourceManagementEndpointBaseUri, 
                account.ActiveDirectoryEndpointBaseUri);
            
            var authContext = GetServicePrincipalContextUri(activeDirectoryEndPoint, tenantId);
            Log.Verbose($"Authentication Context: {authContext}");

            var app = ConfidentialClientApplicationBuilder.Create(applicationId)
                                                          .WithClientSecret(password)
                                                          .WithAuthority(authContext)
                                                          .Build();

            var result = await app.AcquireTokenForClient(new [] { $"{managementEndPoint}/.default" })
                                  .WithTenantId(tenantId)
                                  .ExecuteAsync(cancellationToken)
                                  .ConfigureAwait(false);

            return result.AccessToken;
        }

        static string GetServicePrincipalContextUri(string activeDirectoryEndPoint, string tenantId)
        {
            activeDirectoryEndPoint = activeDirectoryEndPoint.TrimEnd('/');
            
            return $"{activeDirectoryEndPoint}/{tenantId}";
        }
        
        static string GetOidcContextUri(string activeDirectoryEndPoint, string tenantId)
        {
            activeDirectoryEndPoint = activeDirectoryEndPoint.TrimEnd('/');

            return $"{activeDirectoryEndPoint}/{tenantId}/v2.0";
        }
    }
}