using Calamari.Common.Plumbing.Extensions;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Common.Plumbing.Extensions
{
    public class StringExtensionsTests
    {
        [Test]
        public void DoubleQuote() => "blah".EnsureDoubleQuote().Should().Be("\"blah\"");
        
        [Test]
        public void DoubleQuote_AlreadyQuoted() => "\"blah\"".EnsureDoubleQuote().Should().Be("\"blah\"");

        [Test]
        public void DoubleQuote_PredicateFalse() => "blah".EnsureDoubleQuote(s => false).Should().Be("blah");
        
        [Test]
        public void DoubleQuote_PredicateTrue() => "blah".EnsureDoubleQuote(s => true).Should().Be("\"blah\"");
        
        [Test]
        public void DoubleQuoteIfContainsSpaces() => "blah blah".EnsureDoubleQuoteIfContainsSpaces().Should().Be("\"blah blah\"");
        
        [Test]
        public void DoubleQuoteIfContainsSpaces_NoSpaces() => "blah".EnsureDoubleQuoteIfContainsSpaces().Should().Be("blah");
    }
}