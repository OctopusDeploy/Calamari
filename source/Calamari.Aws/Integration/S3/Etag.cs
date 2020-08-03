using System.Text.RegularExpressions;
using Calamari.Common.Plumbing;

namespace Calamari.Aws.Integration.S3
{
    public class ETag
    {
        private static readonly Regex EtagRegex = new Regex("(?<=\")(?<etag>\\w+)(?=\")");
   
        public ETag(string value)
        {
            Guard.NotNullOrWhiteSpace(value, "Etag value should may not be null or empty.");
            var match = EtagRegex.Match(value);
            RawValue = value;
            Hash = match.Success ? match.Groups["etag"].Value : RawValue;
        }

        public string RawValue { get; }

        public string Hash { get; }
    }
}