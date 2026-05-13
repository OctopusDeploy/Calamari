using System;
using Calamari.Common.Commands;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Git;

/// <summary>
/// Lazily registers custom smart sub-transports for libgit2sharp so that the native
/// library is only loaded when git operations are actually needed, rather than during
/// startup.
/// This class supports workers that have either OpenSSL 3 (<c>libcrypto.so.3</c>) or
/// OpenSSL 1.1 (<c>libcrypto.so.1.1</c>) installed. When neither is found, a
/// <see cref="Calamari.Common.Commands.CommandException"/> is raised with a
/// user-actionable message explaining what to install.
/// </summary>
static class LibGit2SharpTransportRegistration
{
    static readonly Lazy<bool> Registered = new(Register);

    public static void EnsureRegistered() => _ = Registered.Value;

    static bool Register() => RegisterWith(() =>
    {
        GlobalSettings.RegisterSmartSubtransport<GitHttpSmartSubTransport>("http");
        GlobalSettings.RegisterSmartSubtransport<GitHttpSmartSubTransport>("https");
    });

    internal static bool RegisterWith(Action registerTransports)
    {
        try
        {
            registerTransports();
        }
        catch (TypeInitializationException ex) when (ex.InnerException is DllNotFoundException dllEx)
        {
            var message = $"""
                           Failed to load the native libgit2 library required for Git operations.

                           On Linux, libgit2 requires OpenSSL 3 (libcrypto.so.3). Install it according to your distribution's guidance or update to a supported OS.
                           If you are running a legacy distribution that does not provide OpenSSL 3, OpenSSL 1.1 (libcrypto.so.1.1) may be used as a transitional fallback, but note that OpenSSL 1.1 is end-of-life and should not be relied upon long-term.

                           Original exception:
                           {dllEx.Message}
                           """;

            throw new CommandException(message, ex);
        }

        return true;
    }
}
