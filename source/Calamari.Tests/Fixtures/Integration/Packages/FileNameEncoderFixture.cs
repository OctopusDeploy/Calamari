using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Packages;
using Calamari.Integration.Packages;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class FileNameEncoderFixture
    {
        [Test]
        [TestCase("Hello", "Hello", Description = "Standard no pecial characters")]
        [TestCase("Hel%lo", "Hel%25lo", Description = "Percent symbol in input  (signal character)")]
        [TestCase("Hel%%lo", "Hel%25%25lo", Description = "Percent symbol in input")]
        [TestCase("Hel%25lo", "Hel%2525lo", Description = "Pre-encoded input double encoded")]
        [TestCase("Hel+lo", "Hel+lo", Description = "Url-unsafe but filename-safe character left alone")]
        [TestCase("1.0.1+beta", "1.0.1+beta", Description = "Standard version name unchanged")]
        public void Encode(string input, string expected)
        {
            Assert.AreEqual(expected, FileNameEscaper.Escape(input));
        }

        [Test]
        public void AllInvalidCharactersEncoded()
        {
            var reallyUnsafeString = "H%<>:\"/\\|?I";
            var encoded = FileNameEscaper.Escape(reallyUnsafeString);

            var encodedCharacters = FileNameEscaper.EscapedCharacters.ToList();
            encodedCharacters.Remove('%'); //Since they are encoded with a %

            Assert.IsFalse(encoded.Any(encodedCharacters.Contains));
        }

        [Test]
        [TestCase("Hello", "Hello")]
        [TestCase("Hel%25lo", "Hel%lo", Description = "Percent decoding (signal character)")]
        [TestCase("Hel%2Flo", "Hel/lo", Description = "Simple character decoding")]
        [TestCase("Hel%2F%5Clo", "Hel/\\lo", Description = "Double encoding decodes")]
        [TestCase("Hel+%/lo", "Hel+%/lo", Description = "Already invalid characters just pass through")]
        [TestCase("1.0.1+beta", "1.0.1+beta", Description = "Standard version name unchanged")]
        public void Decode(string input, string expected)
        {
            Assert.AreEqual(expected, FileNameEscaper.Unescape(input));
        }
    }
}
