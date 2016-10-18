using System;
using System.Runtime.InteropServices;

namespace Calamari
{
    public static class CalamariEnvironment
    {
        /// <summary>
        /// If/When we try executing this on another runtime we can 'tweak' this logic.
        /// The reccomended way to determine at runtime if executing within the mono framework
        /// http://www.mono-project.com/docs/gui/winforms/porting-winforms-applications/#runtime-conditionals
        /// </summary>
        public static readonly bool IsRunningOnMono = Type.GetType("Mono.Runtime") != null;


        /// <summary>
        /// Based on some internal methods used my mono itself
        /// https://github.com/mono/mono/blob/master/mcs/class/corlib/System/Environment.cs
        /// </summary>
        internal static bool IsRunningOnNix
        {
            get
            {
#if NET40
                return Environment.OSVersion.Platform == PlatformID.MacOSX ||
                       Environment.OSVersion.Platform == PlatformID.Unix;
#else
                return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
#endif
            }
        }

        public static bool IsRunningOnWindows
        {
            get
            {
#if NET40
                return Environment.OSVersion.Platform == PlatformID.Win32NT ||
                       Environment.OSVersion.Platform == PlatformID.Win32S ||
                       Environment.OSVersion.Platform == PlatformID.Win32Windows ||
                       Environment.OSVersion.Platform == PlatformID.WinCE;
#else
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
            }
        }
    }
}
