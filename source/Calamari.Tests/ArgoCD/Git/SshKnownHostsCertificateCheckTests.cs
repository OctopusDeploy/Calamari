using System;
using System.Linq;
using System.Security.Cryptography;
using Calamari.ArgoCD.Git;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Git;

[TestFixture]
public class SshKnownHostsCertificateCheckTests
{
    // Base64 of a stable 32-byte public-key blob used across matching tests.
    const string StablePublicKeyBase64 = "AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=";

    static readonly byte[] StablePublicKeyHashed = SHA256.HashData(Convert.FromBase64String(StablePublicKeyBase64));

    static SshKnownHost MakeHost(string host, string publicKeyBase64) =>
        new(host, publicKeyBase64);

    [Test]
    public void TrustedWithMatchingHostAndHash()
    {
        var lookup = new[] { MakeHost("github.com", StablePublicKeyBase64) }
            .ToLookup(h => h.Host, StringComparer.OrdinalIgnoreCase);

        var result = SshKnownHostsCertificateCheck.CheckKnownHosts(lookup, "github.com", StablePublicKeyHashed);

        result.Should().Be(SshKnownHostsCertificateCheck.Result.Trusted);
    }

    [Test]
    public void MismatchWithMatchingHostButWrongHash()
    {
        var different = Convert.FromBase64String(StablePublicKeyBase64);
        different[0] ^= 0xFF;
        var wrongFingerprint = SHA256.HashData(different);

        var lookup = new[] { MakeHost("github.com", StablePublicKeyBase64) }
            .ToLookup(h => h.Host, StringComparer.OrdinalIgnoreCase);

        var result = SshKnownHostsCertificateCheck.CheckKnownHosts(lookup, "github.com", wrongFingerprint);

        result.Should().Be(SshKnownHostsCertificateCheck.Result.KeyMismatch);
    }

    [Test]
    public void UnknownHostWithMissingHost()
    {
        var lookup = new[] { MakeHost("github.com", StablePublicKeyBase64) }
            .ToLookup(h => h.Host, StringComparer.OrdinalIgnoreCase);

        var result = SshKnownHostsCertificateCheck.CheckKnownHosts(lookup, "bitbucket.org", StablePublicKeyHashed);

        result.Should().Be(SshKnownHostsCertificateCheck.Result.UnknownHost);
    }

    [Test]
    public void MalformedKeysDontBreakTheCheck()
    {
        // One bad entry + one good entry for the same host.
        // The bad entry must be silently skipped and the good one matched.
        var badEntry = MakeHost("github.com", "!!!not-valid-base64!!!");
        var goodEntry = MakeHost("github.com", StablePublicKeyBase64);
        var lookup = new[] { badEntry, goodEntry }.ToLookup(h => h.Host, StringComparer.OrdinalIgnoreCase);

        var result = SshKnownHostsCertificateCheck.CheckKnownHosts(lookup, "github.com", StablePublicKeyHashed);

        result.Should().Be(SshKnownHostsCertificateCheck.Result.Trusted);
    }

    [Test]
    public void MalformedConfigurationWhenAllEntriesForHostFailBase64()
    {
        // Host is known, but every entry's PublicKey is invalid base64 — should be distinct from UnknownHost.
        var lookup = new[]
                     {
                         MakeHost("github.com", "!!!not-valid-base64!!!"),
                         MakeHost("github.com", "@@@also-not-valid@@@")
                     }
            .ToLookup(h => h.Host, StringComparer.OrdinalIgnoreCase);

        var result = SshKnownHostsCertificateCheck.CheckKnownHosts(lookup, "github.com", StablePublicKeyHashed);

        result.Should().Be(SshKnownHostsCertificateCheck.Result.MalformedConfiguration);
    }

    [Test]
    public void HostnameMatchIsCaseInsensitive()
    {
        var lookup = new[] { MakeHost("GITHUB.COM", StablePublicKeyBase64) }
            .ToLookup(h => h.Host, StringComparer.OrdinalIgnoreCase);

        var result = SshKnownHostsCertificateCheck.CheckKnownHosts(lookup, "github.com", StablePublicKeyHashed);

        result.Should().Be(SshKnownHostsCertificateCheck.Result.Trusted);
    }

    [Test]
    public void HostnameMatchIgnoresTrailingPort()
    {
        // ssh:// URLs may carry a non-default port (e.g. ssh://git@github.com:2222/repo.git).
        // The known hosts list is stored without ports, so we must strip the trailing :port before lookup.
        var lookup = new[] { MakeHost("github.com", StablePublicKeyBase64) }
            .ToLookup(h => h.Host, StringComparer.OrdinalIgnoreCase);

        var result = SshKnownHostsCertificateCheck.CheckKnownHosts(lookup, "github.com:2222", StablePublicKeyHashed);

        result.Should().Be(SshKnownHostsCertificateCheck.Result.Trusted);
    }
}
