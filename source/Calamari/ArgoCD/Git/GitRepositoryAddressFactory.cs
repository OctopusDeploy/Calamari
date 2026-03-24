using System;
using System.Text.RegularExpressions;

namespace Calamari.ArgoCD.Git;

public static class GitRepositoryAddressFactory
{
    static readonly Regex VariablesRegex = new("#{.+}", RegexOptions.Compiled);

    public static bool ContainsVariable(string input) => VariablesRegex.IsMatch(input);

    public static IGitRepositoryAddressOrVariable FromString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Git repository address cannot be null or empty.", nameof(input));

        if (ContainsVariable(input))
            return new GitRepositoryAddressTemplate(input);

        return new GitRepositoryAddress(input);
    }
}
