using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.S3.Model;
using Calamari.Aws.Integration.S3;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Tests.AWS.S3
{
    [TestFixture]
    public class S3ObjectExtensionsFixture
    {
        [Test]
        public void MetadataComparisonIsTrueWhenHeadersMatch()
        {
            var useHeaderValues = GetKnownSpecialKeyApplier();

            PutObjectRequest request = new PutObjectRequest().Tee(r =>
            {
                useHeaderValues(r.Headers);
            });

            GetObjectMetadataResponse response = new GetObjectMetadataResponse().Tee(r =>
            {
                useHeaderValues(r.Headers);
            });

            var result = S3ObjectExtensions.MetadataEq(request.ToCombinedMetadata(), response.ToCombinedMetadata());
            Assert.IsTrue(result, "Metadata was found to be not equal, but should match.");
        }

        [Test]
        public void MetadataComparisonPicksUpDifferencesInSpecialHeaders()
        {
            var useHeaderValues = GetKnownSpecialKeyApplier();
            var useDifferentHeaderValues = GetKnownSpecialKeyApplier();

            PutObjectRequest request = new PutObjectRequest().Tee(r =>
            {
                useHeaderValues(r.Headers);
            });

            GetObjectMetadataResponse response = new GetObjectMetadataResponse().Tee(r =>
            {
                useDifferentHeaderValues(r.Headers);
            });

            var result = S3ObjectExtensions.MetadataEq(request.ToCombinedMetadata(), response.ToCombinedMetadata());
            Assert.IsFalse(result, "The comparison was found to be equal, however the special headers differ.");
        }


        [Test]
        public void MetadataComparisonIsPicksUpDifferenceInMetadataValues()
        {
            var useHeaderValues = GetKnownSpecialKeyApplier();

            PutObjectRequest request = new PutObjectRequest().Tee(r =>
            {
                useHeaderValues(r.Headers);
                r.Metadata.Add("Cowabunga", "Baby");
            });

            GetObjectMetadataResponse response = new GetObjectMetadataResponse().Tee(r =>
            {
                useHeaderValues(r.Headers);
                r.Metadata.Add("Cowabunga", "Baby2");
            });
            
            var result = S3ObjectExtensions.MetadataEq(request.ToCombinedMetadata(), response.ToCombinedMetadata());
            Assert.IsFalse(result, "Metadata comparison was found to be match, but should differ");
        }

        [Test]
        public void MetadataComparisonIsPicksUpDifferenceInMetadataKeys()
        {
            var useHeaderValues = GetKnownSpecialKeyApplier();

            PutObjectRequest request = new PutObjectRequest().Tee(r =>
            {
                useHeaderValues(r.Headers);
                r.Metadata.Add("Meep Meep", "Baby2");
            });

            GetObjectMetadataResponse response = new GetObjectMetadataResponse().Tee(r =>
            {
                useHeaderValues(r.Headers);
                r.Metadata.Add("Cowabunga", "Baby2");
            });

            var result = S3ObjectExtensions.MetadataEq(request.ToCombinedMetadata(), response.ToCombinedMetadata());
            Assert.IsFalse(result, "Metadata comparison was found to be match, but should differ");
        }

        [Test]
        public void MetadataComparisonIgnoresUnknownSpecialHeaders()
        {
            PutObjectRequest request = new PutObjectRequest().Tee(r =>
            {
                r.Headers["Geppetto"] = "Pinocchio ";

                r.Metadata.Add("Cowabunga", "Baby");
            });

            GetObjectMetadataResponse response = new GetObjectMetadataResponse().Tee(r =>
            {
                r.Headers["Crazy"] = "Town";

                r.Metadata.Add("Cowabunga", "Baby");
            });

            var result = S3ObjectExtensions.MetadataEq(request.ToCombinedMetadata(), response.ToCombinedMetadata());
            Assert.IsTrue(result, "Metadata was found to be equal, but should differ");
        }

        private Action<HeadersCollection> GetKnownSpecialKeyApplier()
        {
            var keys = S3ObjectExtensions.GetKnownSpecialHeaderKeys().Select((x) => new KeyValuePair<string, string>(x, $"value-{Guid.NewGuid()}" )).ToList();

            return (collection) =>
            {
                foreach (var keyValuePair in keys)
                {
                    collection[keyValuePair.Key] = keyValuePair.Value;
                }
            };
        }

    }
}
