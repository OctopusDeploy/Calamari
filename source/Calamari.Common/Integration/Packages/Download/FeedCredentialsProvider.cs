#if USE_NUGET_V2_LIBS
using System;
using System.Collections.Concurrent;
using System.Net;
using NuGet;

namespace Calamari.Integration.Packages.Download
{
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

        class DynamicCachedCredential : CredentialCache, ICredentials
        {
            readonly string url;

            public DynamicCachedCredential(string url)
            {
                this.url = url;
            }

            NetworkCredential ICredentials.GetCredential(Uri uri, string authType)
            {
                var credential = Instance.GetCurrentCredentials(url);
                return credential.GetCredential(uri, authType);
            }
        }
    }
}
#endif