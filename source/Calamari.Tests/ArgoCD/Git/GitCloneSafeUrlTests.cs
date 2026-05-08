using System;
using Calamari.ArgoCD.Git;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Git;


public class GitCloneSafeUrlTests
{
    [TestCase("git@github.com:Foo/Bar.git", "https://github.com/Foo/Bar.git")]
    [TestCase("git@bitbucket.com:FooBar.git", "https://bitbucket.com/FooBar.git")]
    public void FromString_ShouldConvertGitScpAddressToUri(string scpAddress, string expectedUrl)
    {
        var result = GitCloneSafeUrl.FromString(scpAddress);
        result.Should().Be(expectedUrl);
    } 

    [Test]
    public void FromString_ShouldReturnValidUriUnmodified()
    {
        var uri = "https://github.com/Foo/Bar.git";
        var result = GitCloneSafeUrl.FromString(uri);
        result.Should().Be(uri);
    } 
    
    [Test]
    public void FromString_ShouldThrowInvalidGitScpAddress()
    {
        var uri = "git@ihavenopath.com";
        var func = () => GitCloneSafeUrl.FromString(uri);
        func.Should().Throw<FormatException>();
    }

    [Test]
    public void ANonProtocoledString_AutomaticallyAddsOci()
    {
        var uri = "registry-1.docker.io/bitnamicharts";
        var result = GitCloneSafeUrl.FromString(uri);
        result.Should().Be($"oci://{uri}");
    }
}