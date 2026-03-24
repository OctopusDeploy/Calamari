using System;

namespace Calamari.ArgoCD.Git;

public class GitRepositoryAddressTemplate : IGitRepositoryAddressOrVariable
{
    public string Raw { get; }

    public GitRepositoryAddressTemplate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Git repository address template cannot be null or empty.", nameof(input));

        if (!GitRepositoryAddressFactory.ContainsVariable(input))
            throw new ArgumentException("Git repository address template must contain an Octopus variable expression #{...}.", nameof(input));

        Raw = input;
    }

    public override string ToString() => Raw;
}
