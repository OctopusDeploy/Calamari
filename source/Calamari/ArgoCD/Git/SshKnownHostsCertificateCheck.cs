using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Calamari.Common.Plumbing.Logging;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace Calamari.ArgoCD.Git;

public static class SshKnownHostsCertificateCheck
{
    internal enum Result
    {
        Trusted,
        UnknownHost,
        KeyMismatch,
    }

    public static CertificateCheckHandler Build(IReadOnlyList<SshKnownHost> hosts, ILog log)
    {
        var byHost = hosts.ToLookup(entry => entry.Host, StringComparer.OrdinalIgnoreCase);

        return (certificate, _, hostname) =>
        {
            if (certificate is not CertificateSsh sshCertificate)
            {
                log.WarnFormat(
                    "Refusing SSH connection to {0}: SSH known hosts check received a non-SSH certificate ({1})",
                    hostname, certificate.GetType().Name);
                return false;
            }

            if (!sshCertificate.HasSHA256)
            {
                log.WarnFormat("Refusing SSH connection to {0}: SSH server did not provide a SHA256 host key fingerprint", hostname);
                return false;
            }

            switch (CertificateMatchesAnyKnownHost(byHost, hostname, sshCertificate.HashSHA256))
            {
                case Result.Trusted:
                    log.VerboseFormat("SSH host key for {0} matched a known host entry", hostname);
                    return true;
                case Result.UnknownHost:
                    log.WarnFormat(
                        "Refusing SSH connection to {0}: not present in the SSH known hosts configuration. Add an entry under Configuration > SSH Known Hosts to allow this connection.",
                        hostname);
                    return false;
                case Result.KeyMismatch:
                    log.WarnFormat(
                        "Refusing SSH connection to {0}: server presented a host key that does not match any known host entry for that host. This may indicate a man-in-the-middle attack, or the host's key may have been rotated.",
                        hostname);
                    return false;
                default:
                    return false;
            }
        };
    }

    internal static Result CertificateMatchesAnyKnownHost(
        ILookup<string, SshKnownHost> byHost,
        string normalisedHostname,
        byte[] presentedSha256)
    {
        if (!byHost.Contains(normalisedHostname))
        {
            return Result.UnknownHost;
        }

        var candidates = byHost[normalisedHostname]
            .Select(entry => FromBase64OrNull(entry.PublicKey))
            .Where(bytes => bytes is not null)
            .Select(bytes => bytes!)
            .ToArray();
        if (candidates.Length == 0)
        {
            return Result.UnknownHost;
        }

        return candidates.Any(decodedKey => SHA256.HashData(decodedKey).AsSpan().SequenceEqual(presentedSha256))
            ? Result.Trusted
            : Result.KeyMismatch;
    }

    static byte[]? FromBase64OrNull(string publicKey)
    {
        try
        {
            return Convert.FromBase64String(publicKey);
        }
        catch (FormatException)
        {
            // Don't let one bad key cause errors
            return null;
        }
    }
}
