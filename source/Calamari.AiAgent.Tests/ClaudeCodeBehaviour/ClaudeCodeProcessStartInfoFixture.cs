using Calamari.AiAgent.ClaudeCodeBehaviour;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class ClaudeCodeProcessStartInfoFixture
{
    [TestCase("simple", "'simple'")]
    [TestCase("has space", "'has space'")]
    [TestCase("it's", @"'it'\''s'")]
    [TestCase("", "''")]
    [TestCase("a'b'c", @"'a'\''b'\''c'")]
    public void ShellQuote_QuotesCorrectly(string input, string expected)
    {
        ClaudeCodeProcessStartInfo.ShellQuote(input).Should().Be(expected);
    }
}
