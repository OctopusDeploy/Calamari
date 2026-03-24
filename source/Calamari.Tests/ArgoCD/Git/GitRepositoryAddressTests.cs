using System;
using Calamari.ArgoCD.Git;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Git;

[TestFixture]
public class GitRepositoryAddressTests
{
    [TestCase("https://github.com/Foo/Bar.git", "https://github.com/Foo/Bar")]
    [TestCase("https://github.com/Foo/Bar", "https://github.com/Foo/Bar")]
    [TestCase("https://github.com/Foo/Bar/", "https://github.com/Foo/Bar")]
    [TestCase("https://github.com/Foo/Bar.GIT", "https://github.com/Foo/Bar")]
    [TestCase("http://example.com/repo.git", "http://example.com/repo")]
    public void HttpUrls_AreNormalized(string input, string expectedNormalized)
    {
        var address = new GitRepositoryAddress(input);
        address.Raw.Should().Be(input);
        address.Normalized.AbsoluteUri.Should().StartWith(expectedNormalized);
    }

    [TestCase("git@github.com:Foo/Bar.git", "ssh", "github.com", "/Foo/Bar")]
    [TestCase("git@bitbucket.com:FooBar.git", "ssh", "bitbucket.com", "/FooBar")]
    [TestCase("git@github.com:Org/Repo", "ssh", "github.com", "/Org/Repo")]
    public void ScpStyleAddresses_AreNormalizedToSshUri(string input, string expectedScheme, string expectedHost, string expectedPath)
    {
        var address = new GitRepositoryAddress(input);
        address.Raw.Should().Be(input);
        address.Normalized.Scheme.Should().Be(expectedScheme);
        address.Normalized.Host.Should().Be(expectedHost);
        address.Normalized.AbsolutePath.Should().Be(expectedPath);
    }

    [Test]
    public void ScpStyleAddress_PreservesGitUsername()
    {
        var address = new GitRepositoryAddress("git@github.com:Foo/Bar.git");
        address.Normalized.UserInfo.Should().Be("git");
    }

    [TestCase("git@ihavenopath.com")]
    [TestCase("git@")]
    public void InvalidScpAddress_Throws(string input)
    {
        var act = () => new GitRepositoryAddress(input);
        act.Should().Throw<FormatException>();
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void NullOrEmpty_Throws(string input)
    {
        var act = () => new GitRepositoryAddress(input!);
        act.Should().Throw<ArgumentException>();
    }

    [TestCase("not a url at all")]
    public void InvalidUrl_Throws(string input)
    {
        var act = () => new GitRepositoryAddress(input);
        act.Should().Throw<FormatException>();
    }

    [Test]
    public void Equality_SameNormalizedUri_AreEqual()
    {
        var a = new GitRepositoryAddress("https://github.com/Foo/Bar.git");
        var b = new GitRepositoryAddress("https://github.com/Foo/Bar");
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Test]
    public void Equality_DifferentCase_AreEqual()
    {
        var a = new GitRepositoryAddress("https://GitHub.COM/Foo/Bar");
        var b = new GitRepositoryAddress("https://github.com/Foo/Bar");
        a.Should().Be(b);
    }

    [Test]
    public void Equality_DifferentRepos_AreNotEqual()
    {
        var a = new GitRepositoryAddress("https://github.com/Foo/Bar");
        var b = new GitRepositoryAddress("https://github.com/Foo/Baz");
        a.Should().NotBe(b);
    }

    [Test]
    public void Equality_NullOther_NotEqual()
    {
        var a = new GitRepositoryAddress("https://github.com/Foo/Bar");
        a.Equals(null).Should().BeFalse();
    }

    [Test]
    public void ToString_ReturnsRaw()
    {
        var raw = "git@github.com:Foo/Bar.git";
        var address = new GitRepositoryAddress(raw);
        address.ToString().Should().Be(raw);
    }

    [Test]
    public void CanBeUsedAsDictionaryKey()
    {
        var dict = new System.Collections.Generic.Dictionary<GitRepositoryAddress, string>
        {
            [new GitRepositoryAddress("https://github.com/Foo/Bar.git")] = "found"
        };

        dict[new GitRepositoryAddress("https://github.com/Foo/Bar")].Should().Be("found");
    }
}
