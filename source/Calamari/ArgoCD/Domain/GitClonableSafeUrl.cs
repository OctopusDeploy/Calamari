using System;

namespace Calamari.ArgoCD.Domain;

/// <summary>
/// NOTE: This class is a copy from Octopus.Server, ensure that any changes are reflected in both implementations
/// 
/// If the original url is a ssh connection, the format is often presented as git@host...
/// Note the missing schema. As such these cannot be parsed as a <see cref="Uri"/> object.
/// Instead, to allow us to utilize credentials for HTTP git interaction, we attempt to convert the ssh address to a HTTPS one.
/// </summary>
public static class GitCloneSafeUrl
{
    const string ExpectedSshPrefix = "git@";

    public static Uri FromString(string uri)
    {
        if (!uri.StartsWith(ExpectedSshPrefix))
        {
            return new Uri(uri);
        }

        var endpoint = uri.Substring(ExpectedSshPrefix.Length);
        var parts = endpoint.Split(":");
        if (parts.Length > 2)
        {
            throw new FormatException($"Unable to parse URI: {endpoint}");
        }
        var builder = new UriBuilder("https", parts[0])
        {
            Path = parts.Length == 2 ? parts[1] : null
        };
        return builder.Uri;
    }
        
    public static Uri FromUri(Uri uri)
    {
        var originalUri = uri.OriginalString;
        return !originalUri.StartsWith(ExpectedSshPrefix) ? uri : FromString(originalUri);
    }
}