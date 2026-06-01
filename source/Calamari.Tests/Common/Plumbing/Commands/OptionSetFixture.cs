using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Plumbing.Commands.Options;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Common.Plumbing.Commands;

[TestFixture]
public class OptionSetFixture
{
    [Test]
    public void Parse_InlineEquals_AssignsValueToCallback()
    {
        string captured = null;
        var set = new OptionSet().Add("name=", "desc", v => captured = v);

        var remaining = set.Parse(["--name=hello"]);

        captured.Should().Be("hello");
        remaining.Should().BeEmpty();
    }

    [Test]
    public void Parse_NextArgValue_AssignsValueToCallback()
    {
        string captured = null;
        var set = new OptionSet().Add("name=", "desc", v => captured = v);

        var remaining = set.Parse(["--name", "hello"]);

        captured.Should().Be("hello");
        remaining.Should().BeEmpty();
    }

    [Test]
    public void Parse_InlineColon_AssignsValueToCallback()
    {
        string captured = null;
        var set = new OptionSet().Add("name=", "desc", v => captured = v);

        set.Parse(["--name:hello"]);

        captured.Should().Be("hello");
    }

    [Test]
    public void Parse_EmptyInlineValue_AssignsEmptyString()
    {
        string captured = null;
        var set = new OptionSet().Add("name=", "desc", v => captured = v);

        set.Parse(["--name="]);

        captured.Should().Be(string.Empty);
    }

    [TestCase("--")]
    [TestCase("-")]
    [TestCase("/")]
    public void Parse_AllPrefixesAccepted(string prefix)
    {
        string captured = null;
        var set = new OptionSet().Add("name=", "desc", v => captured = v);

        set.Parse([$"{prefix}name=hello"]);

        captured.Should().Be("hello");
    }

    [Test]
    public void Parse_NameMatchingIsCaseInsensitive()
    {
        string captured = null;
        var set = new OptionSet().Add("name=", "desc", v => captured = v);

        set.Parse(["--NAME=hello"]);

        captured.Should().Be("hello");
    }

    [Test]
    public void Parse_BareFlag_InvokesActionWithNonNullArgument()
    {
        string captured = null;
        var set = new OptionSet().Add("verbose", "desc", v => captured = v);

        set.Parse(["--verbose"]);

        captured.Should().NotBeNull();
    }

    [Test]
    public void Parse_BareFlagFollowedByPositional_LeavesPositionalUnconsumed()
    {
        var invocations = 0;
        var set = new OptionSet().Add("verbose", "desc", _ => invocations++);

        var remaining = set.Parse(["--verbose", "leftover"]);

        invocations.Should().Be(1);
        remaining.Should().Equal("leftover");
    }

    [Test]
    public void Parse_UnknownFlag_PassesThroughToRemaining()
    {
        var set = new OptionSet().Add("name=", "desc", _ => { });

        var remaining = set.Parse(new[] { "--unknown", "--name=hello", "positional" });

        remaining.Should().Equal("--unknown", "positional");
    }

    [Test]
    public void Parse_NonFlagArguments_PassThroughToRemaining()
    {
        var set = new OptionSet().Add("name=", "desc", _ => { });

        var remaining = set.Parse(["positional1", "--name=hello", "positional2"]);

        remaining.Should().Equal("positional1", "positional2");
    }

    [Test]
    public void Parse_SameOptionMultipleTimes_InvokesActionEachTime()
    {
        var values = new List<string>();
        var set = new OptionSet().Add("name=", "desc", v => values.Add(v));

        set.Parse(["--name=a", "--name=b", "--name=c"]);

        values.Should().Equal("a", "b", "c");
    }

    [Test]
    public void Parse_RequiredValueOptionWithoutValueAtEnd_Throws()
    {
        var set = new OptionSet().Add("name=", "desc", _ => { });

        Action act = () => set.Parse(["--name"]);

        act.Should().Throw<OptionException>().WithMessage("*name*");
    }

    [Test]
    public void Parse_ValueContainingEquals_PreservesEverythingAfterFirstSeparator()
    {
        string captured = null;
        var set = new OptionSet().Add("kv=", "desc", v => captured = v);

        set.Parse(new[] { "--kv=key=value=extra" });

        captured.Should().Be("key=value=extra");
    }

    [Test]
    public void Parse_NullArguments_Throws()
    {
        var set = new OptionSet();
        // ReSharper disable once AssignNullToNotNullAttribute
        Action act = () => set.Parse(null);
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Add_NullPrototype_Throws()
    {
        var set = new OptionSet();
        // ReSharper disable once AssignNullToNotNullAttribute
        Action act = () => set.Add(null, "desc", _ => { });
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Add_NullAction_Throws()
    {
        var set = new OptionSet();
        // ReSharper disable once AssignNullToNotNullAttribute
        Action act = () => set.Add("name=", "desc", null);
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Add_EmptyPrototype_Throws()
    {
        var set = new OptionSet();
        Action act = () => set.Add("", "desc", _ => { });
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Add_BareEqualsPrototype_Throws()
    {
        var set = new OptionSet();
        Action act = () => set.Add("=", "desc", _ => { });
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Add_ReturnsSetForChaining()
    {
        var set = new OptionSet();

        var returned = set.Add("a=", "desc", _ => { });

        returned.Should().BeSameAs(set);
    }

    [Test]
    public void Add_RegisteringSameNameTwice_LatestActionWins()
    {
        var first = 0;
        var second = 0;
        var set = new OptionSet()
                  .Add("name=", "desc", _ => first++)
                  .Add("name=", "desc", _ => second++);

        set.Parse(["--name=value"]);

        first.Should().Be(0);
        second.Should().Be(1);
    }

    [Test]
    public void WriteOptionDescriptions_RendersRequiredValueOption()
    {
        var set = new OptionSet().Add("package=", "Path to the package.", _ => { });
        var output = WriteToString(set);

        output.Should().Contain("--package=VALUE");
        output.Should().Contain("Path to the package.");
    }

    [Test]
    public void WriteOptionDescriptions_RendersBareFlag()
    {
        var set = new OptionSet().Add("verbose", "Verbose output.", _ => { });
        var output = WriteToString(set);

        output.Should().Contain("--verbose");
        output.Should().Contain("Verbose output.");
    }

    [Test]
    public void WriteOptionDescriptions_OmitsBlankDescription()
    {
        var set = new OptionSet().Add("name=", "", _ => { });
        var output = WriteToString(set).TrimEnd();

        output.Should().Be("  --name=VALUE");
    }

    [Test]
    public void WriteOptionDescriptions_NullWriter_Throws()
    {
        var set = new OptionSet();
        // ReSharper disable once AssignNullToNotNullAttribute
        Action act = () => set.WriteOptionDescriptions(null);
        act.Should().Throw<ArgumentNullException>();
    }

    static string WriteToString(OptionSet set)
    {
        using var writer = new StringWriter();
        set.WriteOptionDescriptions(writer);
        return writer.ToString();
    }
}