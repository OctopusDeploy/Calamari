using System;

namespace Calamari.ArgoCD.Git;

/// <summary>
/// NOTE: This class is copied in Octopus.Server, ensure that any changes are reflected in both implementations
/// </summary>
public static class GitCloneSafeUrl
{
    const string StandardSshScpPrefix = "git@";
    
    /// <summary>
    /// For "historical" reasons, git vendors don't actually publish a compliant SSH URI,
    /// but instead an anachronistic scp-style supported syntax
    /// see https://stackoverflow.com/a/70330178 and https://git-scm.com/book/en/v2/Git-on-the-Server-The-Protocols#_the_ssh_protocol
    ///  
    /// This class ensures that SSH uris are valid <see cref="Uri"/> objects,
    /// and otherwise passes all other values directly though.
    ///
    /// Due to its usage in git cloning alongside other git credentials, the user component of the SCP address (typically `git`) is
    /// dropped from the generated uri
    ///
    /// eg git@github.com:Acme/Corp.git ==> https://github.com/Acme/Corp.git
    /// </summary>
    /// <param name="uri">A, potentially invalid <see cref="Uri"/> object</param>
    /// <returns>A URI that, if is SSH, is well formed<see cref="Uri"/> object</returns>
    public static Uri FromString(string uri)
    {
        if (!uri.StartsWith(StandardSshScpPrefix))
        {
            return new Uri(uri);
        }

        var scpAddress = uri.Substring(StandardSshScpPrefix.Length);
        var parts = scpAddress.Split(':');
        if (parts.Length != 2)
        {
            throw new FormatException($"Unable to parse URI: {uri}");
        }
        var host = parts[0];
        var path = parts[1];

        var uriBuilder = new UriBuilder(){
            Scheme = Uri.UriSchemeHttps,
            Host = host,
            Path = path
        };
        return uriBuilder.Uri;
    }
}