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
                return Regex.Replace(path, @"(?<!\\)%([a-zA-Z0-9_]+)%", "");
            }

            return Environment.ExpandEnvironmentVariables(path);
        }
    }
}