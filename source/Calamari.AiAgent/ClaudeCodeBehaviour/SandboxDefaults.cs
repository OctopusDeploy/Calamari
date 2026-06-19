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
    {
        return variables
               .GetStrings(variableName, '\n', '\r')
               .Concat(defaults)
               .Distinct()
               .ToList();
    }
}