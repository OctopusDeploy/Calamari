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

    const string TestWorkDir = "/tmp/test-workdir";

    static ClaudeCommandArgsBuilder MinimalBuilder() =>
        new ClaudeCommandArgsBuilder()
            .WithPrompt("test prompt")
            .WithModel("claude-sonnet-4-20250514");

    [Test]
    public void ResolveInvocation_NoWrapper_UsesClaude()
    {
        var (fileName, arguments) = ClaudeCodeProcessStartInfo.ResolveInvocation(MinimalBuilder(), null, TestWorkDir);

        fileName.Should().Be("claude");
        arguments.Should().StartWith(" --model");
    }

    [Test]
    public void ResolveInvocation_ClaudeToken_ExpandsToExecutableOnly()
    {
        var (fileName, arguments) = ClaudeCodeProcessStartInfo.ResolveInvocation(MinimalBuilder(), "sbx run {claude}", TestWorkDir);

        fileName.Should().Be("sbx");
        arguments.Should().Be("run claude");
        arguments.Should().NotContain("--model");
    }

    [Test]
    public void ResolveInvocation_SbxFormat_AllThreeTokens()
    {
        var (fileName, arguments) = ClaudeCodeProcessStartInfo.ResolveInvocation(MinimalBuilder(), "sbx run {claude} {workdir} -- {claude-args}", TestWorkDir);

        fileName.Should().Be("sbx");
        arguments.Should().StartWith($"run claude {TestWorkDir} -- --model");
        arguments.Should().NotContain("--  ");
    }

    [Test]
    public void ResolveInvocation_WorkdirToken_ExpandsToWorkingDirectory()
    {
        var (_, arguments) = ClaudeCodeProcessStartInfo.ResolveInvocation(MinimalBuilder(), "sbx run {claude} {workdir} -- {claude-args}", "/my/work/dir");

        arguments.Should().Contain("/my/work/dir");
    }

    [Test]
    public void ResolveInvocation_ClaudeArgsToken_ContainsArgsNotExecutable()
    {
        var (fileName, arguments) = ClaudeCodeProcessStartInfo.ResolveInvocation(MinimalBuilder(), "wrap -- {claude-args}", TestWorkDir);

        fileName.Should().Be("wrap");
        arguments.Should().StartWith("-- --model");
        arguments.Should().NotContain("claude ");
    }
}
