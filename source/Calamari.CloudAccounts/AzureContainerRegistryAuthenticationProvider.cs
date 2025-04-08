using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NetWebRequest = System.Net.WebRequest;

namespace Calamari.CloudAccounts
{
    public class AzureContainerRegistryAuthenticationProvider
    {
        private readonly HttpClient _httpClient;
        
        // Constants for ACR authentication
        private const string AcrUsername = "00000000-0000-0000-0000-000000000000";
        private const string AcrScope = "registry:catalog:* repository:*:pull repository:*:metadata_read";
        private const string AzureDefaultScope = "https://management.azure.com/.default";

        public AzureContainerRegistryAuthenticationProvider()
        {
            _httpClient = new HttpClient(new HttpClientHandler {Proxy = NetWebRequest.DefaultWebProxy});
        }

        public (string Username, string Password, string RegistryUri) GetAcrUserNamePasswordCredentials(string username, string password, IVariables variables)
        {
            var registryUri = variables.Get("feedUri");
            return (username, password, registryUri);
        }
        
        public async Task<(string Username, string Password, string RegistryUri)> GetAcrOidcCredentials(IVariables variables)
        {
            try
            {
                Log.Verbose("Starting ACR OIDC credential retrieval process");
                var jwt = variables.Get(AuthenticationVariables.Jwt);
                var clientId = variables.Get(AuthenticationVariables.Azure.ClientId);
                var tenantId = variables.Get(AuthenticationVariables.Azure.TenantId);
                var registryUri = variables.Get("feedUri");
                var aadToken = await ExchangeJwtForAccessTokenAsync(jwt, clientId, tenantId);
                var refreshToken = await GetAcrRefreshTokenAsync(registryUri, aadToken, tenantId);
                var accessToken = await GetAcrAccessTokenAsync(registryUri, refreshToken);
                Log.Verbose($"Successfully retrieved credentials for {registryUri}");
                return (AcrUsername, accessToken, registryUri);
            }
            catch (Exception ex)
            { 
                throw new Exception($"ACR-LOGIN-ERROR: Failed to verify OIDC credentials. Error: {ex.Message}", ex);
            }
        }

        async Task<string> ExchangeJwtForAccessTokenAsync(string jwt, string clientId, string tenantId)
        {
            var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_assertion", jwt),
                new KeyValuePair<string, string>("client_assertion_type", "urn:ietf:params:oauth:client-assertion-type:jwt-bearer"),
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", AzureDefaultScope)
            });

            using (var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint) { Content = content })
            {
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead))
                {
                    response.EnsureSuccessStatusCode();
                    var responseContent = await response.Content.ReadAsStringAsync();

                    var tokenResponse = JsonConvert.DeserializeObject<JObject>(responseContent);
                    var accessToken = tokenResponse["access_token"]?.ToString();
                    
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        throw new InvalidOperationException("Failed to get access_token from token response");
                    }
                    
                    return accessToken;
                }
            }
        }

        async Task<string> GetAcrRefreshTokenAsync(string registryUri, string aadToken, string tenantId)
        {
            var requestUri = $"{registryUri}/oauth2/exchange";
            using (var request = new HttpRequestMessage(HttpMethod.Post, requestUri))
            {
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "access_token" },
                    { "service", new Uri(registryUri).Host },
                    { "access_token", aadToken },
                    { "tenant", tenantId }
                });

                try
                {
                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            var errorBody = await response.Content.ReadAsStringAsync();
                            throw new InvalidOperationException($"Failed to get ACR refresh token from {requestUri}. Status: {response.StatusCode}. Body: {errorBody}");
                        }

                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        var tokenResponse = JsonConvert.DeserializeObject<JObject>(jsonResponse);
                        var refreshToken = tokenResponse["refresh_token"]?.ToString();
                        
                        if (string.IsNullOrEmpty(refreshToken))
                        {
                            throw new FormatException($"ACR refresh token response from {requestUri} did not contain a valid 'refresh_token' string. Response: {jsonResponse}");
                        }

                        return refreshToken;
                    }
                }
                catch (JsonException jsonEx)
                {
                    throw new FormatException($"Failed to parse ACR refresh token response from {requestUri}. Error: {jsonEx.Message}", jsonEx);
                }
            }
        }

        async Task<string> GetAcrAccessTokenAsync(string registryUri, string refreshToken)
        {
            var requestUri = $"{registryUri}/oauth2/token";
            using (var request = new HttpRequestMessage(HttpMethod.Post, requestUri))
            {
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "service", new Uri(registryUri).Host },
                    { "scope", AcrScope },
                    { "refresh_token", refreshToken }
                });

                try
                {
                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            var errorBody = await response.Content.ReadAsStringAsync();
                            throw new InvalidOperationException($"Failed to get ACR access token from {requestUri}. Status: {response.StatusCode}. Body: {errorBody}");
                        }

                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        var tokenResponse = JsonConvert.DeserializeObject<JObject>(jsonResponse);
                        var accessToken = tokenResponse["access_token"]?.ToString();
                        
                        if (string.IsNullOrEmpty(accessToken))
                        {
                            throw new FormatException($"ACR access token response from {requestUri} did not contain a valid 'access_token' string. Response: {jsonResponse}");
                        }
                        
                        return accessToken;
                    }
                }
                catch (JsonException jsonEx)
                {
                    throw new FormatException($"Failed to parse ACR access token response from {requestUri}. Error: {jsonEx.Message}", jsonEx);
                }
            }
        }
    }
}