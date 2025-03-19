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

namespace Calamari.CloudAccounts
{
    public static class AwsAuthenticationProvider
    {
           public static async Task<(string Username, string Password, string RegistryUri)> GetAwsOidcCredentials(IVariables variables)
        {
            try
            {
                var jwt = variables.Get("Jwt");
                var roleArn = variables.Get("RoleArn");
                var region = variables.Get("Region");
                var sessionDuration = variables.Get("SessionDuration");
                
                var client = new AmazonSecurityTokenServiceClient(new AnonymousAWSCredentials());
                var assumeRoleWithWebIdentityResponse = await client.AssumeRoleWithWebIdentityAsync(new AssumeRoleWithWebIdentityRequest
                {
                    RoleArn = roleArn,
                    DurationSeconds = int.TryParse(sessionDuration, out var seconds) ? seconds : 3600,
                    RoleSessionName = "OctopusAwsAuthentication",
                    WebIdentityToken = jwt
                });
                var credentials = new SessionAWSCredentials(assumeRoleWithWebIdentityResponse.Credentials.AccessKeyId,
                                                            assumeRoleWithWebIdentityResponse.Credentials.SecretAccessKey,
                                                            assumeRoleWithWebIdentityResponse.Credentials.SessionToken);
            
                var regionEndpoint = RegionEndpoint.GetBySystemName(region);
                var ecrClient = new AmazonECRClient(credentials, regionEndpoint);
                var tokenResponse = await ecrClient.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
        
                var authToken = tokenResponse.AuthorizationData.FirstOrDefault();
                if (authToken == null)
                {
                    throw new Exception("No AuthToken found");
                }
                var decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(authToken.AuthorizationToken));
                var parts = decodedToken.Split(':');
        
                if (parts.Length != 2)
                {
                    throw new Exception("Token returned by AWS is in an unexpected format");
                }
        
                return (parts[0], parts[1], authToken.ProxyEndpoint);
            }
            catch (Exception ex)
            {
                // catch the exception and fallback to returning false
                throw new Exception("AWS-LOGIN-ERROR-0005.1: Failed to verify OIDC credentials. "
                                    + $"Error: {ex}");
            }
        }
        
        public static (string Username, string Password, string RegistryUri) GetAwsAccessKeyCredentials(IVariables variables, string accessKey, string secretKey)
        {
            var region = variables.Get("Region");
            var regionEndpoint = RegionEndpoint.GetBySystemName(region);
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            var client = new AmazonECRClient(credentials, regionEndpoint);
            try
            {
                var token = client.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest()).GetAwaiter().GetResult();
                var authToken = token.AuthorizationData.FirstOrDefault();
                if (authToken == null)
                {
                    throw new Exception("No AuthToken found");
                }

                var creds = DecodeCredentials(authToken);
                return (creds.Username, creds.Password, authToken.ProxyEndpoint);
            }
            catch (Exception ex)
            {
                throw new AuthenticationException($"Unable to retrieve AWS Authorization token:\r\n\t{ex.Message}");
            }
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