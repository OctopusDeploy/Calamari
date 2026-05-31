using System;
using System.Runtime.InteropServices;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Plumbing
{
    public static class CalamariEnvironment
    {
        public static bool IsRunningOnKubernetes =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(KubernetesEnvironmentVariables.KubernetesAgentNamespace));

        /// <summary>
        /// Based on some internal methods used my mono itself
        /// https://github.com/mono/mono/blob/master/mcs/class/corlib/System/Environment.cs
        /// </summary>
        public static bool IsRunningOnNix => Environment.OSVersion.Platform == PlatformID.Unix && !IsRunningOnMac;

        public static bool IsRunningOnWindows => Environment.OSVersion.Platform == PlatformID.Win32NT ||
            Environment.OSVersion.Platform == PlatformID.Win32S ||
            Environment.OSVersion.Platform == PlatformID.Win32Windows ||
            Environment.OSVersion.Platform == PlatformID.WinCE;

        public static bool ShouldWaitForDebugger(IVariables variables)
        {
#if DEBUG

            var waitForDebugger = variables.Get(KnownVariables.Calamari.WaitForDebugger);
            var waitForDebuggerInEnv = Environment.GetEnvironmentVariable("_CALAMARI_WAIT_FOR_DEBUGGER");
            
            return string.Equals(waitForDebugger, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(waitForDebuggerInEnv, "true", StringComparison.OrdinalIgnoreCase);
#endif
            return false;
        }

        public static bool IsRunningOnMac
        {
            get
            {
                if (Environment.OSVersion.Platform == PlatformID.MacOSX)
                    return true;
                if (Environment.OSVersion.Platform != PlatformID.Unix)
                    return false;

                var buf = IntPtr.Zero;
                try
                {
                    buf = Marshal.AllocHGlobal(8192);
                    // Get sysname from uname ()
                    if (uname(buf) == 0)
                    {
                        var os = Marshal.PtrToStringAnsi(buf);
                        if (os == "Darwin")
                            return true;
                    }
                }
                catch
                {
                    // ignored
                }
                finally
                {
                    if (buf != IntPtr.Zero)
                        Marshal.FreeHGlobal(buf);
                }

                return false;
            }
        }

        //from https://github.com/jpobst/Pinta/blob/master/Pinta.Core/Managers/SystemManager.cs#L162
        //(MIT license)
        [DllImport("libc")] //From Managed.Windows.Forms/XplatUI
        static extern int uname(IntPtr buf);
    }
}