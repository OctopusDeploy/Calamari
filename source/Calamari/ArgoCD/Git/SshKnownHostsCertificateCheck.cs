using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Calamari.Common.Plumbing.Logging;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Git;

public static class SshKnownHostsCertificateCheck
{
    internal enum Result
    {
        Trusted,
        UnknownHost,
        MalformedConfiguration,
        KeyMismatch,
    }

    public static CertificateCheckHandler Build(IReadOnlyList<SshKnownHost> hosts, ILog log)
    {
        var hasNoConfiguration = hosts.Count == 0;

        // Pre-warn if any of the keys are malformed
        for (var i = 0; i < hosts.Count; i++)
        {
            if (FromBase64OrNull(hosts[i].PublicKey) is null)
            {
                log.WarnFormat(
                    "SSH known hosts entry #{0} for host '{1}' has a PublicKey that is not valid base64 and will be ignored. Check Configuration > SSH Known Hosts.",
                    i,
                    hosts[i].Host);
            }
        }

        var byHost = hosts.ToLookup(entry => entry.Host, StringComparer.OrdinalIgnoreCase);

        return (certificate, _, hostname) =>
               {
                   var sshCertificate = ParseAndValidateCertificate(log, certificate, hostname);
                   if (sshCertificate is null)
                   {
                       return false;
                   }

                   switch (CheckKnownHosts(byHost, hostname, sshCertificate.HashSHA256))
                   {
                       case Result.Trusted:
                           log.VerboseFormat("SSH host key for {0} matched a known host entry", hostname);
                           return true;
                       case Result.UnknownHost when hasNoConfiguration:
                           log.WarnFormat(
                               "Refusing SSH connection to {0}: no SSH known hosts have been configured.",
                               hostname);
                           return false;
                       case Result.UnknownHost:
                           log.WarnFormat(
                               "Refusing SSH connection to {0}: not present in the SSH known hosts configuration. Add an entry under Configuration > SSH Known Hosts to allow this connection.",
                               hostname);
                           return false;
                       case Result.MalformedConfiguration:
                           log.WarnFormat(
                               "Refusing SSH connection to {0}: every SSH known hosts entry for this host has a PublicKey that is not valid base64. Check Configuration > SSH Known Hosts.",
                               hostname);
                           return false;
                       case Result.KeyMismatch:
                           log.WarnFormat(
                               "Refusing SSH connection to {0}: server presented a host key that does not match any known host entry for that host. This may indicate a man-in-the-middle attack, or the host's key may have been rotated.",
                               hostname);
                           return false;
                       default:
                           throw new ArgumentOutOfRangeException(nameof(Result), "Unhandled Result value");
                   }
               };
    }

    static CertificateSsh? ParseAndValidateCertificate(ILog log, Certificate certificate, string hostname)
    {
        if (certificate is null)
        {
            log.WarnFormat("Refusing SSH connection to {0}: no certificate presented", hostname);
            return null;
        }

        if (certificate is not CertificateSsh sshCertificate)
        {
            log.WarnFormat(
                "Refusing SSH connection to {0}: SSH known hosts check received a non-SSH certificate ({1})",
                hostname,
                certificate.GetType().Name);
            return null;
        }

        if (!sshCertificate.HasSHA256)
        {
            log.WarnFormat("Refusing SSH connection to {0}: SSH server did not provide a SHA256 host key fingerprint", hostname);
            return null;
        }

        return sshCertificate;
    }

    internal static Result CheckKnownHosts(
        ILookup<string, SshKnownHost> byHost,
        string hostname,
        byte[] presentedSha256)
    {
        var lookupHost = StripPort(hostname);

        if (!byHost.Contains(lookupHost))
        {
            return Result.UnknownHost;
        }

        var candidates = byHost[lookupHost].Select(entry => FromBase64OrNull(entry.PublicKey)).WhereNotNull().ToArray();
        if (candidates.Length == 0)
        {
            return Result.MalformedConfiguration;
        }

        return candidates.Any(decodedKey => SHA256.HashData(decodedKey).AsSpan().SequenceEqual(presentedSha256))
            ? Result.Trusted
            : Result.KeyMismatch;
    }

    static string StripPort(string hostname)
    {
        if (string.IsNullOrEmpty(hostname))
        {
            return hostname;
        }

        // IPv6 literals are bracketed: "[::1]:22" -> "[::1]".
        if (hostname[0] == '[')
        {
            var close = hostname.IndexOf(']');
            return close > 0 ? hostname.Substring(0, close + 1) : hostname;
        }

        var colon = hostname.IndexOf(':');
        return colon >= 0 ? hostname[..colon] : hostname;
    }

    static byte[]? FromBase64OrNull(string publicKey)
    {
        try
        {
            return Convert.FromBase64String(publicKey);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}