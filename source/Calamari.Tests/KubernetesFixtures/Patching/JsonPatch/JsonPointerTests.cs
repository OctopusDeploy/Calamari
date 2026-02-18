using System;
using Calamari.Kubernetes.Patching.JsonPatch;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

[TestFixture]
public class JsonPointerTests
{
    [Test]
    public void StoresValueAndCanRoundTrip()
    {
        var pointer = new JsonPointer("/foo/bar");
        pointer.Tokens.Should().Equal(["foo", "bar"]);
        pointer.ToString().Should().Be("/foo/bar");
    }
    
    [Test]
    public void StoresValueAndCanRoundTripEmptyString()
    {
        var pointer = new JsonPointer("");
        pointer.Tokens.Should().Equal([]);
        pointer.ToString().Should().Be("");
    }
    
    [Test]
    public void StoresValueAndCanRoundTripBareSlash()
    {
        var pointer = new JsonPointer("/");
        pointer.Tokens.Should().Equal([""]);
        pointer.ToString().Should().Be("/");
    }
    
    [Test]
    public void StoresValueAndCanRoundTripWithEscaping()
    {
        var pointer = new JsonPointer("/foo/m~0n/a~1b/bar");
        pointer.Tokens.Should().Equal(["foo", "m~n", "a/b", "bar"]);
        pointer.ToString().Should().Be("/foo/m~0n/a~1b/bar");
    }

    [Test]
    public void EqualsWhenValuesSame()
    {
        var pointer1 = new JsonPointer("/foo");
        var pointer2 = new JsonPointer("/foo");

        pointer1.Should().Be(pointer2);
        (pointer1 == pointer2).Should().BeTrue();
    }

    [Test]
    public void NotEqualsWhenValuesDifferent()
    {
        var pointer1 = new JsonPointer("/foo");
        var pointer2 = new JsonPointer("/bar");

        pointer1.Should().NotBe(pointer2);
        (pointer1 != pointer2).Should().BeTrue();
    }

    [Test]
    public void HashCodeSameForEqualPointers()
    {
        var pointer1 = new JsonPointer("/foo");
        var pointer2 = new JsonPointer("/foo");

        pointer1.GetHashCode().Should().Be(pointer2.GetHashCode());
    }

    [Test]
    public void SupportsEmptyString()
    {
        var pointer = new JsonPointer("");
        pointer.Tokens.Should().Equal([]);
    }
    
    [Test]
    public void NullIsNotAllowed()
    {
        var act = () => new JsonPointer((string)null!);
        act.Should().Throw<ArgumentNullException>();
        
        var act2 = () => new JsonPointer((string[])null!);
        act2.Should().Throw<ArgumentNullException>();
    }
}
