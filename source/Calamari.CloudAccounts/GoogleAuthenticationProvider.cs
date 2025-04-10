using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using NetWebRequest = System.Net.WebRequest;

namespace Calamari.CloudAccounts
{
    public class GoogleAuthenticationProvider
    {
        readonly HttpClient httpClient;
        const string StsEndpoint = "https://sts.googleapis.com/v1/token";
        const string GcrScope = "https://www.googleapis.com/auth/devstorage.read_write";
        const string OidcUsername = "oauth2accesstoken";
        
        public GoogleAuthenticationProvider()
        {
            httpClient = new HttpClient(new HttpClientHandler {Proxy = NetWebRequest.DefaultWebProxy});
        }
        
        public (string Username, string Password, Uri RegistryUri) GetGcrUserNamePasswordCredentials(string username, string password, Uri registryUri)
        {
            return (username, password, registryUri);
        }
        
        public async Task<(string Username, string Password, Uri RegistryUri)> GetGcrOidcCredentials(IVariables variables, Uri registryUri)
        {
            try
            {
                Log.Verbose("Starting GCR OIDC credential retrieval process");
                var jwt = variables.Get(AuthenticationVariables.Jwt);
                var rawAudience = variables.Get(AuthenticationVariables.Google.Audience);
                var audience = Uri.TryCreate(rawAudience, UriKind.Absolute, out var uri)
                    ? $"//{uri.Host}{uri.AbsolutePath}"
                    : rawAudience;
                var gcrToken = await ExchangeJwtForGcrToken(jwt, audience);
                Log.Verbose($"Successfully retrieved credentials for {registryUri}");
                return (OidcUsername, gcrToken, registryUri);
            }
            catch (Exception ex)
            { 
                throw new Exception($"GCR-LOGIN-ERROR: Failed to verify OIDC credentials. Error: {ex.Message}", ex);
            }
        }
        
        async Task<string> ExchangeJwtForGcrToken(string jwt, string audience)
        {
            
            var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "urn:ietf:params:oauth:grant-type:token-exchange" },
                { "audience", audience },
                { "scope", GcrScope },
                { "requested_token_type", "urn:ietf:params:oauth:token-type:access_token" },
                { "subject_token", jwt },
                { "subject_token_type", "urn:ietf:params:oauth:token-type:jwt" }
            });

            var response = await httpClient.PostAsync(StsEndpoint, requestContent, CancellationToken.None);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to exchange JWT for GCR token. Status: {response.StatusCode}, Error: {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseJson);
    
            if (string.IsNullOrEmpty(tokenResponse?.AccessToken))
            {
                throw new Exception("Access token was not present in the response");
            }
    
            return tokenResponse.AccessToken;
        }
        
        class TokenResponse
        {
            [JsonProperty("access_token")]
            public string AccessToken { get; set; }
    
            [JsonProperty("token_type")]
            public string TokenType { get; set; }
    
            [JsonProperty("expires_in")]
            public int? ExpiresIn { get; set; }
    
            [JsonProperty("issued_token_type")]
            public string IssuedTokenType { get; set; }
        }
    }
}