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

    const string TestSandboxRuntimeSettingsPath = "/tmp/test-workdir/.srt-settings.json";
    const string TestBashSettingsPath = "/tmp/test-workdir/.claude/settings.sandbox.json";

    static ClaudeCommandArgsBuilder MinimalBuilder() =>
        new ClaudeCommandArgsBuilder()
            .WithPrompt("test prompt")
            .WithModel("claude-sonnet-4-20250514");

    [Test]
    public void ResolveInvocation_NoneMode_RunsClaudeDirectly()
    {
        var (fileName, arguments) = ClaudeCodeProcessStartInfo.ResolveInvocation(MinimalBuilder().WithSandboxMode(SandboxMode.None));

        fileName.Should().Be("claude");
        arguments.Should().StartWith(" --model");
        arguments.Should().NotContain("srt");
    }

    [Test]
    public void ResolveInvocation_BashMode_PassesSettingsFlag()
    {
        var builder = MinimalBuilder().WithSandboxMode(SandboxMode.Bash).WithSettingsPath(TestBashSettingsPath);

        var (fileName, arguments) = ClaudeCodeProcessStartInfo.ResolveInvocation(builder);

        fileName.Should().Be("claude");
        arguments.Should().Contain($"--settings {TestBashSettingsPath}");
        arguments.Should().NotContain("srt");
    }

    [Test]
    public void ResolveInvocation_SandboxRuntimeMode_WrapsClaudeWithSrt()
    {
        var builder = MinimalBuilder().WithSandboxMode(SandboxMode.SandboxRuntime).WithSandboxRuntimeSettingsPath(TestSandboxRuntimeSettingsPath).WithPrompt("look! a \"quote\"");

        var (fileName, arguments) = ClaudeCodeProcessStartInfo.ResolveInvocation(builder);

        fileName.Should().Be("srt");
        // The claude invocation is passed as a single escaped argument to srt's -c flag.
        arguments.Should().StartWith($"--settings {TestSandboxRuntimeSettingsPath} -c \"claude --model");
        arguments.Should().Contain("-p \\\"look! a \\\\\\\"quote\\\\\\\"\\\"");
        arguments.Should().EndWith("\"");
    }

    [Test]
    public void ResolveInvocation_SandboxRuntimeMode_WithoutSettingsPath_Throws()
    {
        var act = () => ClaudeCodeProcessStartInfo.ResolveInvocation(MinimalBuilder().WithSandboxMode(SandboxMode.SandboxRuntime));

        act.Should().Throw<System.InvalidOperationException>();
    }
}