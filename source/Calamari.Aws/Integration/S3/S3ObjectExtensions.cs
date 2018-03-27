using System.Collections.Generic;
using System.Linq;
using Amazon.S3.Model;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Integration.S3
{
    public static class S3ObjectExtensions
    {
        public static List<Tag> ToTagSet(this IEnumerable<KeyValuePair<string, string>> source)
        {
            return source?.Select(x => new Tag {Key = x.Key.Trim(), Value = x.Value?.Trim()}).ToList() ?? new List<Tag>();
        }

        public static PutObjectRequest WithMetadata(this PutObjectRequest request, IEnumerable<KeyValuePair<string, string>> source)
        {
            return request.Tee(x =>
            {
                foreach (var item in source)
                {
                    x.Metadata.Add(item.Key.Trim(), item.Value?.Trim());
                }
            });
        }

        public static PutObjectRequest WithMetadata(this PutObjectRequest request, IHaveMetadata source)
        {
            return request.WithMetadata(source.Metadata);
        }

        public static PutObjectRequest WithTags(this PutObjectRequest request, IHaveTags source)
        {
            return request.Tee(x => x.TagSet = source.Tags.ToTagSet());
        }
    }
}
