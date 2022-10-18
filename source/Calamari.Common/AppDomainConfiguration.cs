using System;

namespace Calamari.Common
{
    internal static class AppDomainConfiguration
    {
        public static readonly TimeSpan DefaultRegexMatchTimeout = TimeSpan.FromSeconds(1);
        
        /// <summary>
        /// Adds default max timout to avoid ReDoS attacks
        /// </summary>
        public static void SetDefaultRegexMatchTimeout() 
            => AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", DefaultRegexMatchTimeout);
    }
}