using System;
using System.Net;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace Calamari.ArgoCD.Git;

public static class LibGit2SharpCredentialsHandlerExtensionMethods
{
    public static CredentialsHandler ToLibGit2SharpCredentialHandler(this IGitConnection? connection)
    {
        return connection switch
               {
                   HttpsGitConnection https => UsernamePassword(https),
                   SshKeyGitConnection sshKey => SshKey(sshKey),
                   null => Anonymous(),
                   _ => throw new NotSupportedException(),
               };
    }

    public static CertificateCheckHandler? ToLibGit2SharpCertificateCheckHandler(this IGitConnection? connection)
    {
        return connection switch
               {
                   SshKeyGitConnection sshKey => SshHostKeyVerificationBypass.AcceptAll,
                   _ => null
               };
    }

    static CredentialsHandler Anonymous()
    {
        return null!; // A null CredentialsHandler is valid for LibGit2Sharp
    }

    static CredentialsHandler SshKey(SshKeyGitConnection connection)
    {
        return (_, userFromUrl, types) =>
               {
                   if (!types.HasFlag(SupportedCredentialTypes.SshMemory))
                   {
                       throw new InvalidOperationException("SSH key credentials provided but are not supported by this endpoint.");
                   }

                   return new SshKeyMemoryCredentials
                   {
                       Username = connection.Username ?? userFromUrl,
                       PrivateKey = connection.PrivateKey,
                   };
               };
    }

    static CredentialsHandler UsernamePassword(HttpsGitConnection connection)
    {
        return (_, _, types) =>
               {
                   if (!types.HasFlag(SupportedCredentialTypes.UsernamePassword))
                   {
                       throw new InvalidOperationException("Username/password credentials provided but are not supported by this endpoint.");
                   }

                   var securePassword = new NetworkCredential(string.Empty, connection.Password).SecurePassword;
                   return new SecureUsernamePasswordCredentials
                   {
                       Username = connection.Username,
                       Password = securePassword,
                   };
               };
    }
}