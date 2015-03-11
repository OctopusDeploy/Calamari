using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NuGet;
using Octopus.Deploy.Startup;

namespace Octopus.Deploy.PackageDownloader
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                string packageId = null;
                string packageVersion = null;
                string feedUri = null;
                string feedUsername = null;
                string feedPassword = null;

                var options = new OptionSet();
                options.Add("packageId=", "Package ID to download", v => packageId = v);
                options.Add("packageVersion=", "Package version to download", v => packageVersion = v);
                options.Add("feedUri=", "URL to NuGet feed", v => feedUri = v);
                options.Add("feedUsername=", "[Optional] Username to use for an authenticated NuGet feed", v => feedUsername = v);
                options.Add("feedPassword=", "[Optional] Password to use for an authenticated NuGet feed", v => feedPassword = v);

                SemanticVersion version;
                Uri uri;
                CheckArguments(
                    packageId, 
                    packageVersion, 
                    feedUri, 
                    feedUsername, 
                    feedPassword, 
                    out version, 
                    out uri);

                var credentials = GetFeedCredentials(feedUsername, feedPassword);
                FeedCredentialsProvider.Instance.SetCredentials(uri, credentials);
                HttpClient.DefaultCredentialProvider = FeedCredentialsProvider.Instance;


            }
            catch (Exception ex)
            {
                return ConsoleFormatter.PrintError(ex);
            }
            return 0;
        }

        private static ICredentials GetFeedCredentials(string feedUsername, string feedPassword)
        {
            ICredentials credentials = CredentialCache.DefaultNetworkCredentials;
            if (!String.IsNullOrWhiteSpace(feedUsername))
            {
                credentials = new NetworkCredential(feedUsername, feedPassword);
            }
            return credentials;
        }

        // ReSharper disable UnusedParameter.Local
        private static void CheckArguments(string packageId, string packageVersion, string feedUri, string feedUsername,
            // ReSharper restore UnusedParameter.Local
            string feedPassword, out SemanticVersion version, out Uri uri)
        {
            if (String.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException("No package ID was specified");
            }

            if (String.IsNullOrWhiteSpace(packageVersion))
            {
                throw new ArgumentException("No package version was specified");
            }

            if (!SemanticVersion.TryParse(packageVersion, out version))
            {
                throw new ArgumentException("Package version specified is not a valid semantic version");
            }

            if (String.IsNullOrWhiteSpace(feedUri))
            {
                throw new ArgumentException("No feed URI was specified");
            }

            if (!Uri.TryCreate(feedUri, UriKind.RelativeOrAbsolute, out uri))
            {
                throw new ArgumentException("URI specified is not a valid URI");
            }

            if (!String.IsNullOrWhiteSpace(feedUsername) && String.IsNullOrWhiteSpace(feedPassword))
            {
                throw new ArgumentException("A username was specified but no password was provided");
            }
        }
    }
    public class FeedCredentialsProvider : ICredentialProvider
    {
        FeedCredentialsProvider()
        {
        }

        public static FeedCredentialsProvider Instance = new FeedCredentialsProvider();
        static readonly ConcurrentDictionary<string, ICredentials> Credentials = new ConcurrentDictionary<string, ICredentials>();
        static readonly ConcurrentDictionary<string, RetryTracker> Retries = new ConcurrentDictionary<string, RetryTracker>();

        public void SetCredentials(Uri uri, ICredentials credential)
        {
            Credentials[Canonicalize(uri)] = credential;
        }

        public ICredentials GetCredentials(Uri uri, IWebProxy proxy, CredentialType credentialType, bool retrying)
        {
            var url = Canonicalize(uri);
            var retry = Retries.GetOrAdd(url, _ => new RetryTracker());

            if (!retrying)
            {
                retry.Reset();
            }
            else
            {
                var retryAllowed = retry.AttemptRetry();
                if (!retryAllowed)
                    return null;
            }

            return new DynamicCachedCredential(url);
        }

        ICredentials GetCurrentCredentials(string url)
        {
            ICredentials credential;
            if (!Credentials.TryGetValue(url, out credential))
            {
                credential = CredentialCache.DefaultNetworkCredentials;
            }

            return credential;
        }

        string Canonicalize(Uri uri)
        {
            return uri.Authority.ToLowerInvariant().Trim();
        }

        public class RetryTracker
        {
            const int MaxAttempts = 3;
            int currentCount;

            public bool AttemptRetry()
            {
                if (currentCount > MaxAttempts) return false;

                currentCount++;
                return true;
            }

            public void Reset()
            {
                currentCount = 0;
            }
        }

        class DynamicCachedCredential : ICredentials
        {
            readonly string url;

            public DynamicCachedCredential(string url)
            {
                this.url = url;
            }

            public NetworkCredential GetCredential(Uri uri, string authType)
            {
                var credential = Instance.GetCurrentCredentials(url);
                return credential.GetCredential(uri, authType);
            }
        }
    }
}
