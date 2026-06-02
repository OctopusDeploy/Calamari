using System;
using Calamari.ArgoCD.Git;
using Calamari.Common.Commands;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Git;

[TestFixture]
public class LibGit2SharpTransportRegistrationTests
{
    [Test]
    public void RegisterWith_WhenDelegateThrowsTypeInitializationExceptionWithDllNotFoundException_ThrowsCommandExceptionWithOpenSslGuidance()
    {
        var dllNotFoundException = new DllNotFoundException("libcrypto.so.3");
        var typeInitEx = new TypeInitializationException("SomeType", dllNotFoundException);

        Action act = () => LibGit2SharpTransportRegistration.RegisterWith(() => throw typeInitEx);

        act.Should().Throw<CommandException>()
           .WithMessage("*OpenSSL 3*")
           .WithMessage("*end-of-life*");
    }

    [Test]
    public void RegisterWith_WhenDelegateSucceeds_ReturnsTrue()
    {
        var result = LibGit2SharpTransportRegistration.RegisterWith(() => { });

        result.Should().BeTrue();
    }
}
