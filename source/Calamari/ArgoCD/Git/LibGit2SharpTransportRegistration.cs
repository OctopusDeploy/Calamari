using System;
using Calamari.Common.Commands;
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
    static readonly Lazy<bool> Registered = new(Register);

    public static void EnsureRegistered() => _ = Registered.Value;

    static bool Register()
    {
        try
        {
            GlobalSettings.RegisterSmartSubtransport<GitHttpSmartSubTransport>("http");
            GlobalSettings.RegisterSmartSubtransport<GitHttpSmartSubTransport>("https");
        }
        catch (TypeInitializationException ex) when (ex.InnerException is DllNotFoundException dllEx)
        {
            var message = $"""
                           Failed to load the native libgit2 library required for Git operations.

                           On Linux, libgit2 requires OpenSSL 3 (libcrypto.so.3) to be installed on the worker.
                           Please install it according to your distributions guidance or update to a supported OS.

                           Original exception:
                           {dllEx.Message}
                           """;

            throw new CommandException(message, ex);
        }

        return true;
    }
}