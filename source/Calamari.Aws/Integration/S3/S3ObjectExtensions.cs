
 using System.Collections.Generic;
using System.Linq;
using Amazon.S3.Model;
﻿using System;
using System.Security.Cryptography;
 using Calamari.Common.Plumbing.Extensions;
 using Calamari.Common.Plumbing.FileSystem;
 using Calamari.Integration.FileSystem;
using Calamari.Util;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;
using Tag = Amazon.S3.Model.Tag;

namespace Calamari.Aws.Integration.S3
{
    public static class S3ObjectExtensions
    {
        //Special headers as per AWS docs - https://docs.aws.amazon.com/AmazonS3/latest/API/RESTObjectPUT.html
        private static readonly IDictionary<string, Func<HeadersCollection, string>> SupportedSpecialHeaders =
            new Dictionary<string, Func<HeadersCollection, string>>(StringComparer.OrdinalIgnoreCase)
                .WithHeaderFrom("Cache-Control", headers => headers.CacheControl)
                .WithHeaderFrom("Content-Disposition", headers => headers.ContentDisposition)
                .WithHeaderFrom("Content-Encoding", headers => headers.ContentEncoding)
                .WithHeaderFrom("Content-Type", headers => headers.ContentType)
                .WithHeaderFrom("Expires")
                .WithHeaderFrom("x-amz-website-redirect-location")
                .WithHeaderFrom("x-amz-object-lock-retain-until-date")
                .WithHeaderFrom("x-amz-object-lock-legal-hold")
                .WithHeaderFrom("x-amz-object-lock-mode");
        
        private static T WithHeaderFrom<T>(this T values, string key) where T : IDictionary<string, Func<HeadersCollection, string>>
        {
            values.Add(key, (headers) => headers[key]);
            return values;
        }

        private static T WithHeaderFrom<T>(this T values, string key, Func<HeadersCollection, string> accessor) where T : IDictionary<string, Func<HeadersCollection, string>>
        {
            values.Add(key, accessor);
            return values;
        }

        public static IEnumerable<string> GetKnownSpecialHeaderKeys()
        {
            return SupportedSpecialHeaders.Keys;
        }

        public static bool MetadataEq(IDictionary<string, string> next, IDictionary<string, string> current)
        {
            var allKeys = next.Keys.Union(current.Keys).Distinct().ToList();
            var keysInBoth = allKeys.Where(key => next.ContainsKey(key) && current.ContainsKey(key)).ToList();
            var missingKeys = allKeys.Except(keysInBoth).ToList();
            var differentValues = keysInBoth.Where(key => !string.Equals(next[key], current[key], StringComparison.OrdinalIgnoreCase)).ToList();

            return missingKeys.Count == 0 && differentValues.Count == 0;
        }

        public static bool HasSameMetadata(this PutObjectRequest request, GetObjectMetadataResponse response)
        {
            return MetadataEq(request.ToCombinedMetadata(), response.ToCombinedMetadata());
        }

        public static IDictionary<string, string> ToDictionary(this MetadataCollection collection)
        {
            return collection.Keys.ToDictionary(key => key, key => collection[key]);
        }

        public static IDictionary<string, string> GetCombinedMetadata(HeadersCollection headers, MetadataCollection metadata)
        {
            var result = new Dictionary<string, string>();

            foreach (var key in headers.Keys.Where(SupportedSpecialHeaders.ContainsKey))
            {
                result.Add(key, SupportedSpecialHeaders[key](headers));
            }

            foreach (var key in metadata.Keys)
            {
                result.Add(key, metadata[key]);
            }

            return result;
        }

        public static IDictionary<string, string> ToCombinedMetadata(this PutObjectRequest request)
        {
            return GetCombinedMetadata(request.Headers, request.Metadata);
        }

        public static IDictionary<string, string> ToCombinedMetadata(this GetObjectMetadataResponse response)
        {
            return GetCombinedMetadata(response.Headers, response.Metadata);
        }

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
                    var key = item.Key.Trim();
                    if (!SupportedSpecialHeaders.ContainsKey(key))
                    {
                        x.Metadata.Add(key, item.Value?.Trim());
                    }
                    else
                    {
                        x.Headers[key] = item.Value?.Trim();
                    }
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
            return !fileSystem.FileExists(path) ? Maybe<byte[]>.None : Maybe<byte[]>.Some(HashCalculator.Hash(path, MD5.Create));
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
            return Convert.FromBase64String(request.MD5Digest).Map(BinaryExtensions.ToHexString);
        }

        public static ETag GetEtag(this GetObjectMetadataResponse metadata)
        {
            return new ETag(metadata.ETag);
        }

        public static bool IsSameAsRequestMd5Digest(this ETag etag, PutObjectRequest request)
        {
            return string.Compare(etag.Hash, request.Md5DigestToHexString(), StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}
