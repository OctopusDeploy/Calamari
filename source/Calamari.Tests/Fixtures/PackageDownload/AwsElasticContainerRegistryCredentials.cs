#pragma warning disable CS8601 // Possible null reference assignment
#pragma warning disable CS8618 // Non-nullable property {0} is uninitialized
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using Amazon;
using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.Runtime;
using JetBrains.Annotations;
using SharpCompress;

namespace Calamari.Tests.Fixtures.PackageDownload
{
    // Taken from Server
    public class AwsElasticContainerRegistryCredentials
    {
        /// <summary>
        /// AWS credentials expire after 12 hours: https://docs.aws.amazon.com/AmazonECR/latest/userguide/Registries.html
        /// Rather than hit the AWS service every time we want to use the registry, we can cache them locally based on the expiry time in the response
        /// </summary>
        static readonly ConcurrentDictionary<(string accessKey, string region), TemporaryCredentials> CachedCreds = new ConcurrentDictionary<(string accessKey, string region), TemporaryCredentials>();

        public virtual TemporaryCredentials RetrieveTemporaryCredentials(string accessKey, string secretKey, string region, bool bypassCache = false)
        {
            if (!bypassCache)
            {
                if (TryUseCache(accessKey, region, out var cachedCreds)) return cachedCreds;
            }

            var authToken = GetAuthorizationData(accessKey, secretKey, region);
            var creds = DecodeCredentials(authToken);
            var tempCreds = new TemporaryCredentials
            {
                Expiry = authToken.ExpiresAt,
                RegistryUri = authToken.ProxyEndpoint,
                Password = creds.Password,
                Username = creds.Username
            };

            return CachedCreds.AddOrUpdate((accessKey, region), tempCreds, (tuple, credentials) => tempCreds);
        }

        static bool TryUseCache(string accessKey, string region, out TemporaryCredentials temporaryCredentials)
        {
            // AWS Creds have 12 hour expiry
            // to ensure it doesnt expire mid-deploy lets make sure we have at least 2 hours left
            CachedCreds.ToArray()
                .Where(kvp => kvp.Value.Expiry.AddHours(-2) < DateTime.UtcNow)
                .ForEach(k => CachedCreds.TryRemove(k.Key, out var _));

            return CachedCreds.TryGetValue((accessKey, region), out temporaryCredentials);
        }

        (string Username, string Password) DecodeCredentials(AuthorizationData authToken)
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

        static AuthorizationData GetAuthorizationData(string accessKey, string secretKey, string region)
        {
            var regionEndpoint = RegionEndpoint.GetBySystemName(region);
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            var client = new AmazonECRClient(credentials, regionEndpoint);
            try
            {
                var token = client.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest()).Result;
                var authToken = token.AuthorizationData.FirstOrDefault();
                if (authToken == null)
                {
                    throw new Exception("No AuthToken found");
                }

                return authToken;
            }
            catch (Exception ex)
            {
                throw new AuthenticationException($"Unable to retrieve AWS Authorization token:\r\n\t{ex.Message}");
            }
        }

        public class TemporaryCredentials
        {
            public string Username { get; set; }

            [CanBeNull]
            public string Password { get; set; }

            public string RegistryUri { get; set; }
            public DateTime Expiry { get; set; }
        }
    }
}
