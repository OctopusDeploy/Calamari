using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

static class SandboxDefaults
{
    // Credential and secret directories that are always denied for reads.
    internal static readonly IReadOnlyList<string> DenyRead =
    [
        "~/.ssh", "~/.aws", "~/.azure", "~/.config/gcloud", "~/.kube", "~/.docker",
        "~/.config/gh", "~/.git-credentials", "~/.netrc", "~/.npmrc", "~/.gnupg",
        "~/.claude/.credentials.json",
        // Additional conservative entries: git config, common secret-manager dirs.
        "~/.config/git", "~/.config/op", "~/.terraform.d",
    ];

    // Anthropic endpoints required for Claude Code to function.
    internal static readonly IReadOnlyList<string> AllowedDomains =
    [
        "api.anthropic.com",
        "statsig.anthropic.com",
    ];

    // Paths the agent is allowed to write to by default.
    internal static readonly IReadOnlyList<string> AllowWrite =
    [
        ".",
        "/tmp",
    ];

    internal static List<string> Merge(IVariables variables, string variableName, IReadOnlyList<string> defaults)
        => variables.GetStrings(variableName, '\n', '\r').Concat(defaults).Distinct().ToList();

    internal static bool? OptionalFlag(IVariables variables, string variableName)
        => variables.IsSet(variableName) ? variables.GetFlag(variableName) : null;

    internal static SandboxNetwork BuildNetworkOptions(
        IVariables variables,
        string allowedDomainsVar,
        string deniedDomainsVar,
        string allowUnixSocketsVar,
        string allowAllUnixSocketsVar,
        string allowLocalBindingVar,
        string httpProxyPortVar,
        string socksProxyPortVar)
        => new()
        {
            AllowedDomains = Merge(variables, allowedDomainsVar, AllowedDomains),
            DeniedDomains = Merge(variables, deniedDomainsVar, []),
            AllowUnixSockets = variables.IsSet(allowUnixSocketsVar) ? variables.GetStrings(allowUnixSocketsVar, '\n', '\r') : null,
            AllowAllUnixSockets = OptionalFlag(variables, allowAllUnixSocketsVar),
            AllowLocalBinding = OptionalFlag(variables, allowLocalBindingVar),
            HttpProxyPort = variables.GetInt32(httpProxyPortVar),
            SocksProxyPort = variables.GetInt32(socksProxyPortVar),
        };

    internal static SandboxFilesystem BuildFilesystemOptions(
        IVariables variables,
        string allowWriteVar,
        string denyWriteVar,
        string denyReadVar,
        string allowReadVar)
        => new()
        {
            AllowWrite = Merge(variables, allowWriteVar, AllowWrite),
            DenyWrite = Merge(variables, denyWriteVar, []),
            DenyRead = Merge(variables, denyReadVar, DenyRead),
            AllowRead = Merge(variables, allowReadVar, []),
        };
}