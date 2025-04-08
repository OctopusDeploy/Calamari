using System;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.CloudAccounts
{
    public static class AwsAuthenticationProvider
    {
        const string DefaultSessionName = "OctopusAwsAuthentication";
        public static async Task<(string Username, string Password, string RegistryUri)> GetEcrOidcCredentials(IVariables variables)
        {
            try
            {
                var jwt = variables.Get(AuthenticationVariables.Jwt);
                var roleArn = variables.Get(AuthenticationVariables.Aws.RoleArn);
                var region = variables.Get(AuthenticationVariables.Aws.Region);
                var sessionDuration = variables.Get(AuthenticationVariables.Aws.SessionDuration);
                
                var client = new AmazonSecurityTokenServiceClient(new AnonymousAWSCredentials());
                var assumeRoleWithWebIdentityResponse = await client.AssumeRoleWithWebIdentityAsync(new AssumeRoleWithWebIdentityRequest
                {
                    RoleArn = roleArn,
                    DurationSeconds = int.TryParse(sessionDuration, out var seconds) ? seconds : 3600,
                    RoleSessionName = DefaultSessionName,
                    WebIdentityToken = jwt
                });
                var credentials = new SessionAWSCredentials(assumeRoleWithWebIdentityResponse.Credentials.AccessKeyId,
                                                            assumeRoleWithWebIdentityResponse.Credentials.SecretAccessKey,
                                                            assumeRoleWithWebIdentityResponse.Credentials.SessionToken);
                var regionEndpoint = RegionEndpoint.GetBySystemName(region);
                var ecrClient = new AmazonECRClient(credentials, regionEndpoint);
                var authToken = await GetAuthorizationData(ecrClient);
                var creds = DecodeCredentials(authToken);
                return (creds.Username, creds.Password, authToken.ProxyEndpoint);
            }
            catch (Exception ex)
            {
                // catch the exception and fallback to returning false
                throw new Exception("AWS-LOGIN-ERROR-0005.1: Failed to verify OIDC credentials. "
                                    + $"Error: {ex}");
            }
        }
        
        public static async Task<(string Username, string Password, string RegistryUri)> GetEcrAccessKeyCredentials(IVariables variables, string accessKey, string secretKey)
        {
            var region = variables.Get(AuthenticationVariables.Aws.Region);
            var regionEndpoint = RegionEndpoint.GetBySystemName(region);
            AmazonECRClient client;
            if (accessKey.IsNullOrEmpty() || secretKey.IsNullOrEmpty())
            {
                client = new AmazonECRClient(regionEndpoint);
            }
            else
            {
                var credentials = new BasicAWSCredentials(accessKey, secretKey);
                client = new AmazonECRClient(credentials, regionEndpoint);
            }
            try
            {
                var authToken = await GetAuthorizationData(client);
                var creds = DecodeCredentials(authToken);
                return (creds.Username, creds.Password, authToken.ProxyEndpoint);
            }
            catch (Exception ex)
            {
                throw new AuthenticationException($"Unable to retrieve AWS Authorization token:\r\n\t{ex.Message}");
            }
        }

        static async Task<AuthorizationData> GetAuthorizationData(AmazonECRClient client)
        {
            var token = await client.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
            var authToken = token.AuthorizationData.FirstOrDefault();
            if (authToken == null)
            {
                throw new Exception("No AuthToken found");
            }
            
            return authToken;
        }
        
        static (string Username, string Password) DecodeCredentials(AuthorizationData authToken)
        {
            try
            {
                var decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(authToken.AuthorizationToken));
                var parts = decodedToken.Split(':');
                if (parts.Length != 2)
                {
                    throw new AuthenticationException("Token returned by AWS is in an unexpected format");
                }

                return (parts[0], parts[1]);
            }
            catch (Exception)
            {
                throw new AuthenticationException("Token returned by AWS is in an unexpected format");
            }
        }
    }
}