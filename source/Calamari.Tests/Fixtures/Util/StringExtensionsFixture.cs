using System;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using Calamari.Util;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Util
{
    [TestFixture]
    public class StringExtensionsFixture
    {
        [TestCase("Hello World", "ello", true)]
        [TestCase("Hello World", "ELLO", true)]
        [TestCase("HELLO WORLD", "ello", true)]
        [TestCase("Hello, world", "lO, wOrL", true)]
        [TestCase("Hello, world", "abc", false)]
        [TestCase("Hello, world", "ABC", false)]
        [TestCase("Hello, world", "!@#", false)]
        [Test]
        public void ShouldContainString(string originalString, string value, bool expected)
        {
            Assert.AreEqual(expected, originalString.ContainsIgnoreCase(value));
        }

        [Test]
        public void NullValueShouldThrowException()
        {
            Action action = () => "foo".ContainsIgnoreCase(null);
            action.Should().Throw<ArgumentNullException>();
        }

        // This method has some weird legacy issues which we now rely on
        [TestCase(@"C:\Path\To\File1.txt", @"C:\Path\", @"To/File1.txt")]
        [TestCase(@"C:\Path\To\File2.txt", @"C:\Path", @"To/File2.txt")]
        [TestCase(@"C:\Path\To\File3 With Spaces.txt", @"C:\Path", @"To/File3 With Spaces.txt")]
        [TestCase(@"C:/Path/To/File4.txt", @"C:/Path", @"To/File4.txt")]
        [TestCase(@"C:/Path/To/File5 With Spaces.txt", @"C:/Path", @"To/File5 With Spaces.txt")]
        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void AsRelativePathFrom(string source, string baseDirectory, string expected)
        {
            Assert.AreEqual(expected, source.AsRelativePathFrom(baseDirectory));
        }
    }
}