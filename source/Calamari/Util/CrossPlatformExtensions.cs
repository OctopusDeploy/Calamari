using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using NuGet;
using Calamari.Util.Environments;

namespace Calamari.Util
{
    public static class CrossPlatform
    {
        public static string ExpandPathEnvironmentVariables(string path)
        {
            if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
            {
                if (path.StartsWith("~"))
                {
                    path = "$HOME" + path.Substring(1, path.Length - 1);
                }

                path = Regex.Replace(path, @"(?<!\\)\$([a-zA-Z0-9_]+)", "%$1%");
                path = Environment.ExpandEnvironmentVariables(path);
                return Regex.Replace(path, @"(?<!\\)%([a-zA-Z0-9_]+)%", "");
            }

            return Environment.ExpandEnvironmentVariables(path);
        }


#if NET40
        public static Type GetTypeInfo(this Type type)
        {
            return type;
        }
#endif
    }
}