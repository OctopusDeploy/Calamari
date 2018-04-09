
﻿using System.Collections.Generic;
using System.Linq;
using Amazon.S3.Model;
﻿using System;
 using Calamari.Integration.FileSystem;
using Calamari.Util;
using Octopus.CoreUtilities;
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

        private static Maybe<byte[]> GetMd5Checksum(ICalamariFileSystem fileSystem, string path)
        {
            return !fileSystem.FileExists(path) ? Maybe<byte[]>.None : Maybe<byte[]>.Some(MD5HashHelper.GetFileMd5Checksum(fileSystem, path));
        }

        private static string ToHexString(byte[] data)
        {
            return BitConverter.ToString(data)
                .Replace("-", string.Empty)
                .ToLower();
        }

        public static PutObjectRequest WithMd5Digest(this PutObjectRequest request, ICalamariFileSystem fileSystem, bool overwrite = false)
        {
            if (!string.IsNullOrEmpty(request.MD5Digest) && !overwrite)
                return request;

            var checksum = GetMd5Checksum(fileSystem, request.FilePath);
            if (!checksum.Some()) return request;

            request.MD5Digest = Convert.ToBase64String(checksum.Value);
            return request;
        }

        public static string Md5DigestToHexString(this PutObjectRequest request)
        {
            return Convert.FromBase64String(request.MD5Digest).Map(ToHexString);
        }

        public static ETag GetEtag(this GetObjectMetadataResponse metadata)
        {
            return new ETag(metadata.ETag);
        }

        public static bool IsSameAsRequestDigest(this ETag etag, PutObjectRequest request)
        {
            return string.Compare(etag.Hash, request.Md5DigestToHexString(), StringComparison.InvariantCultureIgnoreCase) == 0;
        }
    }
}
