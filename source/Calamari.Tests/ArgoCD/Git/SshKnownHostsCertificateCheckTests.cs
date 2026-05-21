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
    // A stable RSA public-key blob (32 bytes) used across matching tests.
    static readonly byte[] StablePublicKeyBytes =
    {
        0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
        0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
        0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18,
        0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20
    };

    static readonly byte[] StablePublicKeyHashed = SHA256.HashData(StablePublicKeyBytes);

    static readonly string StablePublicKeyBase64 = Convert.ToBase64String(StablePublicKeyBytes);

    static SshKnownHost MakeHost(string host, string publicKeyBase64) =>
        new(host, publicKeyBase64);

    [Test]
    public void TrustedWithMatchingHostAndHash()
    {
        var lookup = new[] { MakeHost("github.com", StablePublicKeyBase64) }
            .ToLookup(h => h.Host, StringComparer.OrdinalIgnoreCase);

        var result = SshKnownHostsCertificateCheck.CertificateMatchesAnyKnownHost(lookup, "github.com", StablePublicKeyHashed);

        result.Should().Be(SshKnownHostsCertificateCheck.Result.Trusted);
    }

    [Test]
    public void MismatchWithMatchingHostButWrongHash()
    {
        var different = (byte[])StablePublicKeyBytes.Clone();
        different[0] ^= 0xFF;
        var wrongFingerprint = SHA256.HashData(different);

        var lookup = new[] { MakeHost("github.com", StablePublicKeyBase64) }
            .ToLookup(h => h.Host, StringComparer.OrdinalIgnoreCase);

        var result = SshKnownHostsCertificateCheck.CertificateMatchesAnyKnownHost(lookup, "github.com", wrongFingerprint);

        result.Should().Be(SshKnownHostsCertificateCheck.Result.KeyMismatch);
    }

    [Test]
    public void UnknownHostWithMissingHost()
    {
        var lookup = new[] { MakeHost("github.com", StablePublicKeyBase64) }
            .ToLookup(h => h.Host, StringComparer.OrdinalIgnoreCase);

        var result = SshKnownHostsCertificateCheck.CertificateMatchesAnyKnownHost(lookup, "bitbucket.org", StablePublicKeyHashed);

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

        var result = SshKnownHostsCertificateCheck.CertificateMatchesAnyKnownHost(lookup, "github.com", StablePublicKeyHashed);

        result.Should().Be(SshKnownHostsCertificateCheck.Result.Trusted);
    }
}