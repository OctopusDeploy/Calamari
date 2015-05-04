using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
                return Environment.OSVersion.Platform == PlatformID.MacOSX ||
                       Environment.OSVersion.Platform == PlatformID.Unix;
            }
        }

    }
}
