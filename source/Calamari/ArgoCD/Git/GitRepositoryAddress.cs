using System;

namespace Calamari.ArgoCD.Git;

public class GitRepositoryAddress : IGitRepositoryAddressOrVariable, IEquatable<GitRepositoryAddress>
{
    const string StandardSshScpPrefix = "git@";

    public string Raw { get; }
    public Uri Normalized { get; }

    public GitRepositoryAddress(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Git repository address cannot be null or empty.", nameof(input));

        Raw = input;
        Normalized = Normalize(input);
    }

    static Uri Normalize(string input)
    {
        Uri uri;

        if (input.StartsWith(StandardSshScpPrefix))
        {
            var scpAddress = input.Substring(StandardSshScpPrefix.Length);
            var colonIndex = scpAddress.IndexOf(':');
            if (colonIndex < 1)
                throw new FormatException($"Unable to parse SCP-style git address: {input}");

            var host = scpAddress.Substring(0, colonIndex);
            var path = scpAddress.Substring(colonIndex + 1);

            if (string.IsNullOrEmpty(path))
                throw new FormatException($"Unable to parse SCP-style git address (empty path): {input}");

            var builder = new UriBuilder
            {
                Scheme = "ssh",
                Host = host,
                Path = path,
                UserName = "git"
            };
            uri = builder.Uri;
        }
        else if (!Uri.TryCreate(input, UriKind.Absolute, out uri!))
        {
            throw new FormatException($"Unable to parse git repository address: {input}");
        }

        return StripTrailingSlash(StripGitSuffix(uri));
    }

    static Uri StripTrailingSlash(Uri uri)
    {
        if (uri.AbsolutePath.EndsWith("/") && uri.AbsolutePath.Length > 1)
        {
            var builder = new UriBuilder(uri);
            builder.Path = builder.Path.TrimEnd('/');
            return builder.Uri;
        }
        return uri;
    }

    static Uri StripGitSuffix(Uri uri)
    {
        const string gitExtension = ".git";
        if (uri.AbsolutePath.EndsWith(gitExtension, StringComparison.OrdinalIgnoreCase))
        {
            var builder = new UriBuilder(uri);
            builder.Path = builder.Path[..^gitExtension.Length];
            return builder.Uri;
        }
        return uri;
    }

    public bool Equals(GitRepositoryAddress? other)
    {
        if (other is null) return false;
        return string.Equals(Normalized.AbsoluteUri, other.Normalized.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => Equals(obj as GitRepositoryAddress);
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Normalized.AbsoluteUri);
    public override string ToString() => Raw;
}
