using System;
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
            originalString.ContainsIgnoreCase(value);
        }

        [Test]
        public void NullValueShouldThrowException()
        {
            Action action = () => "foo".ContainsIgnoreCase(null);
            action.ShouldThrow<ArgumentNullException>();
        }
    }
}