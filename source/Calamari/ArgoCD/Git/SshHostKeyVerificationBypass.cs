namespace Calamari.ArgoCD.Git;

internal static class SshHostKeyVerificationBypass
{
    // TODO(eddy): Implement proper host key verification
    public static readonly LibGit2Sharp.Handlers.CertificateCheckHandler AcceptAll = (cert, valid, host) => true;
}
