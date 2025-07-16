using System;
using System.Text.RegularExpressions;

namespace Calamari.Common.Plumbing.Extensions
{
    public static class CrossPlatform
    {
        public static string ExpandPathEnvironmentVariables(string path)
        {
            if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
            {
                if (path.StartsWith("~"))
                    path = "$HOME" + path.Substring(1, path.Length - 1);

                path = Regex.Replace(path, @"(?<!\\)\$([a-zA-Z0-9_]+)", "%$1%");
                path = Environment.ExpandEnvironmentVariables(path);
                
                // Special case: If the path contains "%2F", which represents an encoded forward slash,
                // we assume it's part of a URL-encoded filename and not an unexpanded environment variable.
                // In this case, we return the path as is to avoid incorrectly modifying the filename.
                // Example: "~/Octopus/Files/Data%2FFileTransferService%2FFileTransferService@S11.0.zip"
                // Here, %2F is part of the filename and should not be treated as an environment variable.
                if (path.Contains("%2F") || path.Contains("%2f"))
                {
                    return path;
                }
        
                // Clean up any remaining unexpanded environment variables.
                // This removes any %VAR% patterns that weren't expanded, 
                // ensuring only valid, expanded variables remain in the path.
                return Regex.Replace(path, @"(?<!\\)%([a-zA-Z0-9_]+)%", "");
            }

            return Environment.ExpandEnvironmentVariables(path);
        }
    }
}