using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.S3.Model;
using Calamari.Aws.Integration.S3;
using NUnit.Framework;

namespace Calamari.Aws.Tests.S3
{
    [TestFixture]
    public class S3ObjectExtensionsFixture
    {
        [Test]
        public void MetadataComparisonIsTrueWhenHeadersMatch()
        {
            var useHeaderValues = GetKnownSpecialKeyApplier();

            var request = new PutObjectRequest();
            useHeaderValues(request.Headers);

            var response = new GetObjectMetadataResponse();
            useHeaderValues(response.Headers);

            var result = S3ObjectExtensions.MetadataEq(request.ToCombinedMetadata(), response.ToCombinedMetadata());
            Assert.IsTrue(result, "Metadata was found to be not equal, but should match.");
        }

        [Test]
        public void MetadataComparisonPicksUpDifferencesInSpecialHeaders()
        {
            var useHeaderValues = GetKnownSpecialKeyApplier();
            var useDifferentHeaderValues = GetKnownSpecialKeyApplier();

            var request = new PutObjectRequest();
            useHeaderValues(request.Headers);

            var response = new GetObjectMetadataResponse();
            useDifferentHeaderValues(response.Headers);

            var result = S3ObjectExtensions.MetadataEq(request.ToCombinedMetadata(), response.ToCombinedMetadata());
            Assert.IsFalse(result, "The comparison was found to be equal, however the special headers differ.");
        }


        [Test]
        public void MetadataComparisonIsPicksUpDifferenceInMetadataValues()
        {
            var useHeaderValues = GetKnownSpecialKeyApplier();

            var request = new PutObjectRequest();
            useHeaderValues(request.Headers);
            request.Metadata.Add("Cowabunga", "Baby");

            var response = new GetObjectMetadataResponse();
            useHeaderValues(response.Headers);
            response.Metadata.Add("Cowabunga", "Baby2");
            
            var result = S3ObjectExtensions.MetadataEq(request.ToCombinedMetadata(), response.ToCombinedMetadata());
            Assert.IsFalse(result, "Metadata comparison was found to be match, but should differ");
        }

        [Test]
        public void MetadataComparisonIsPicksUpDifferenceInMetadataKeys()
        {
            var useHeaderValues = GetKnownSpecialKeyApplier();

            var request = new PutObjectRequest();
            useHeaderValues(request.Headers);
            request.Metadata.Add("Meep Meep", "Baby2");

            var response = new GetObjectMetadataResponse();
            useHeaderValues(response.Headers);
            response.Metadata.Add("Cowabunga", "Baby2");

            var result = S3ObjectExtensions.MetadataEq(request.ToCombinedMetadata(), response.ToCombinedMetadata());
            Assert.IsFalse(result, "Metadata comparison was found to be match, but should differ");
        }

        [Test]
        public void MetadataComparisonIgnoresUnknownSpecialHeaders()
        {
            var request = new PutObjectRequest
            {
                Headers = {["Geppetto"] = "Pinocchio "}
            };
            request.Metadata.Add("Cowabunga", "Baby");

            var response = new GetObjectMetadataResponse
            {
                Headers = { ["Crazy"] = "Town " }
            };
            response.Metadata.Add("Cowabunga", "Baby");

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
