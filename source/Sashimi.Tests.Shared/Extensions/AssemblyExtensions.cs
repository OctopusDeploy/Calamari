using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Sashimi.Tests.Shared.Extensions
{
    public static class EmbeddedResourceExtensions
    {
        public static string ReadResourceAsString(this object test, string postfix)
        {
            var name = test.GetType().Namespace + "." + postfix;
            using (var stream = test.GetType().Assembly.GetManifestResourceStream(name))
            using (var sr = new StreamReader(stream ?? throw new Exception($"Could not find the resource {name}")))
            {
                return sr.ReadToEnd();
            }
        }

        public static string FullLocalPath(this Assembly assembly)
        {
            string str = Uri.UnescapeDataString(new UriBuilder(assembly.Location ?? throw new ArgumentException("Assembly is null", nameof(assembly))).Path);
            if (PlatformDetection.IsRunningOnWindows)
                str = str.Replace("/", "\\");
            return str;
        }
    }

    public static class PlatformDetection
    {
        public static bool IsRunningOnNix => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public static bool IsRunningOnWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static bool IsRunningOnMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    }
}