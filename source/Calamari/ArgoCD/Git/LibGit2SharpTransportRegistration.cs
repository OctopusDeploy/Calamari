using System;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Git;

/// <summary>
/// Lazily registers custom smart sub-transports for libgit2sharp so that the native
/// library is only loaded when git operations are actually needed, rather than during
/// startup.
/// The only reason not to do it during startup is that we have a new dependency on
/// OpenSSL3 that older (now unsupported) OS versions may not fulfill. Instead of
/// breaking everyone if they are running older systems, we will only break them
/// if they use git functionality.
/// </summary>
static class LibGit2SharpTransportRegistration
{
    static readonly Lazy<bool> Registered = new(() =>
        {
            GlobalSettings.RegisterSmartSubtransport<GitHttpSmartSubTransport>("http");
            GlobalSettings.RegisterSmartSubtransport<GitHttpSmartSubTransport>("https");
            return true;
        });

    public static void EnsureRegistered() => _ = Registered.Value;
}