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
        public static string GetApplicationTempDir()
        {
#if NET40
            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
#else
            var path = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? Environment.GetEnvironmentVariable("TMPDIR") ?? "/tmp";
#endif
            path = Path.Combine(path, Assembly.GetEntryAssembly()?.GetName().Name);
            path = Path.Combine(path, "Temp");
            return path;
        }

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

        public static Encoding GetDefaultEncoding()
        {
#if HAS_DEFAULT_ENCODING
            return Encoding.Default;
#else
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(0); // returns windows-1251 for windows
#endif
        }

        public static void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName)
        {
#if NET40
            File.Replace(sourceFileName, destinationFileName, destinationBackupFileName);
#else
            File.Move(destinationFileName, destinationBackupFileName);
            File.Move(sourceFileName, destinationFileName);
#endif
        }

        public static string GetPackageExtension()
        {
#if USE_NUGET_V2_LIBS
            return Constants.PackageExtension;
#else
            return ".nupkg";
#endif
        }

        public static string GetManifestExtension()
        {
#if USE_NUGET_V2_LIBS
            return Constants.ManifestExtension;
#else
            return ".nuspec";
#endif
        }

        public static string GetCurrentDirectory()
        {
#if NET40
            return Environment.CurrentDirectory;
#else
            return Directory.GetCurrentDirectory();
#endif
        }

        public static string GetCommonApplicationDataFolderPath()
        {
#if NET40
            return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
#else
            if (IsWindows())
            {
                return Environment.GetEnvironmentVariable("PROGRAMDATA") ?? "C:\\ProgramData";
            }
            else
            {
                return "/usr/share"; //based on what mono does
            }
#endif
        }

        public static string GetApplicationDataFolderPath()
        {
#if NET40
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
#else
            if (IsWindows())
            {
                var appdata = Environment.GetEnvironmentVariable("APPDATA");
                if(appdata != null) return appdata;

                var home = Environment.GetEnvironmentVariable("USERPROFILE");
                return Path.Combine(home, "AppData", "Roaming");
            }
            else
            {
                //based on what mono does
                var home = Environment.GetEnvironmentVariable("HOME");
                return Path.Combine(home, ".config");
            }
#endif
        }

        public static string GetSystemFolderPath()
        {
#if NET40
            return Environment.GetFolderPath(Environment.SpecialFolder.System);
#else
            if (IsWindows())
            {
                var system = Environment.GetEnvironmentVariable("SYSTEMROOT") ?? "C:\\Windows";
                return Path.Combine(system, "System32");
            }
            else
            {
                return "/usr/bin"; //does not make much sense in nix
            }
#endif
        }

        public static string GetUserDomainName()
        {
#if NET40
            return Environment.UserDomainName;
#else
            if (IsWindows())
            {
                return Environment.GetEnvironmentVariable("USERDOMAIN");
            }
            else
            {
                return Environment.GetEnvironmentVariable("HOSTNAME"); // seems as usefull as anything
            }
#endif            
        }

        public static string GetUserName()
        {
#if NET40
            return Environment.UserName;
#else
            return Environment.GetEnvironmentVariable("USERNAME") ?? Environment.GetEnvironmentVariable("USER");
#endif                
        }

        public static bool IsWindows()
        {
#if NET40
            return System.Environment.OSVersion.Platform == PlatformID.Win32NT
                || System.Environment.OSVersion.Platform == PlatformID.Win32S
                || System.Environment.OSVersion.Platform == PlatformID.Win32Windows;
#else
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
        }

        public static string GetHomeFolder()
        {
            return IsWindows()
                ? Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%")
                : Environment.GetEnvironmentVariable("HOME");
        }

#if NET40
        public static Type GetTypeInfo(this Type type)
        {
            return type;
        }
#endif
    }
}