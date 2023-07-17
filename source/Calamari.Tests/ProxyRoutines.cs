//Copied from https://github.com/chentiangemalc/ProxyUtils/blob/master/SetProxy/ProxyRoutines.cs

/*
 * by Malcolm McCaffery http://chentiangemalc.wordpress.com
 * Twitter @chentiangemalc
 * free to use as you wish...just if you make it better, send me the fixes! :)
 * 
 * Based on information from:
 * 
 *  - WinInet.H in Windows SDK
 *  - MSDN InternetSetOption Documentation http://msdn.microsoft.com/en-us/library/windows/desktop/aa385114(v=vs.85).aspx
 *  - Frame of the native interop calls borrowed from Mudasir Mirza's post here http://stackoverflow.com/questions/9319906/set-proxy-username-and-password-using-wininet-in-c-sharp
 *
 *  Most common failure from InternetSetOption will be error code '87' which means "Invalid Parameter" 
 *  
 * Other things to watch out for:
 * 
 * If you have per-machine proxy policy set, you must be admin to change proxy refer to http://msdn.microsoft.com/en-us/library/ms815135.aspx
 */

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Calamari.Tests
{
    static class ProxyRoutines
    {
        #region WinInet Structures
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct InternetPerConnOptionList : IDisposable
        {
            public int dwSize;              // size of INTERNET_PER_CONN_OPTION_LIST struct
            public IntPtr szConnection;     // connection name to set/query options
            public int dwOptionCount;       // number of options to set/query
            public int dwOptionError;       // on error, which option failed
            public IntPtr options;

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            // The bulk of the clean-up code is implemented in Dispose(bool)
            private void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (szConnection != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(szConnection);
                        szConnection= IntPtr.Zero;
                    }

                    if (options != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(options);
                        options = IntPtr.Zero;

                    }
                }
            }
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct InternetConnectionOption
        {
            static readonly int Size = Marshal.SizeOf(typeof(InternetConnectionOption));
            public PerConnOption m_Option;
            public InternetConnectionOptionValue m_Value;
            
            // Nested Types
            [StructLayout(LayoutKind.Explicit)]
            public struct InternetConnectionOptionValue : IDisposable
            {
                // Fields
                [FieldOffset(0)]
                public System.Runtime.InteropServices.ComTypes.FILETIME m_FileTime;
                [FieldOffset(0)]
                public int m_Int;
                [FieldOffset(0)]
                public IntPtr m_StringPtr;

                public void Dispose()
                {
                    Dispose(true);
                    GC.SuppressFinalize(this);
                }

                // The bulk of the clean-up code is implemented in Dispose(bool)
                private void Dispose(bool disposing)
                {
                    if (disposing)
                    {
                        if (m_StringPtr != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(m_StringPtr);
                            m_StringPtr = IntPtr.Zero;
                        }
                    }
                }


            }
        }
        #endregion

        #region WinInet enums
        //
        // options manifests for Internet{Query|Set}Option
        //
        private enum InternetOption : uint
        {
            INTERNET_OPTION_REFRESH = 0x00000025,
            INTERNET_OPTION_SETTINGS_CHANGED = 0x00000027,
            INTERNET_OPTION_PROXY_SETTINGS_CHANGED = 0x0000005F,
            INTERNET_OPTION_PER_CONNECTION_OPTION = 0x0000004B
        }

        //
        // Options used in INTERNET_PER_CONN_OPTON struct
        //
        private enum PerConnOption : int
        {
            INTERNET_PER_CONN_FLAGS = 1,            // Sets or retrieves the connection type. The Value member will contain one or more of the values from PerConnFlags 
            INTERNET_PER_CONN_PROXY_SERVER = 2,     // Sets or retrieves a string containing the proxy servers.  
            INTERNET_PER_CONN_PROXY_BYPASS = 3,     // Sets or retrieves a string containing the URLs that do not use the proxy server.  
            INTERNET_PER_CONN_AUTOCONFIG_URL = 4,   // Sets or retrieves a string containing the URL to the automatic configuration script.  
            INTERNET_PER_CONN_AUTODISCOVERY_FLAGS = 5 // Sets or AutoDiscovery Flags
        }

        //
        // PER_CONN_FLAGS
        //
        [Flags]
        private enum PerConnFlags : int
        {
            PROXY_TYPE_DIRECT = 0x00000001,         // direct to net
            PROXY_TYPE_PROXY = 0x00000002,          // via named proxy
            PROXY_TYPE_AUTO_PROXY_URL = 0x00000004, // autoproxy URL
            PROXY_TYPE_AUTO_DETECT = 0x00000008     // use autoproxy detection
        }

        [Flags]
        private enum AutoDetectFlags : int
        {
            AUTO_PROXY_FLAG_USER_SET = 0x00000001,
            AUTO_PROXY_FLAG_ALWAYS_DETECT = 0x00000002,
            AUTO_PROXY_FLAG_DETECTION_RUN = 0x00000004,
            AUTO_PROXY_FLAG_MIGRATED = 0x00000008,
            AUTO_PROXY_FLAG_DONT_CACHE_PROXY_RESULT = 0x00000010,
            AUTO_PROXY_FLAG_CACHE_INIT_RUN = 0x00000020,
            AUTO_PROXY_FLAG_DETECTION_SUSPECT = 0x00000040
        }
        #endregion

        private static class NativeMethods
        {
            [DllImport("WinInet.dll", SetLastError = true, CharSet = CharSet.Auto)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool InternetSetOption(IntPtr hInternet, InternetOption dwOption, IntPtr lpBuffer, int dwBufferLength);
        }

        public static bool SetProxy(bool EnableProxy)
        {
            return SetProxy(EnableProxy, false, "", "", "","");
        }

        public static bool SetProxy(string ProxyAddress)
        {
            return SetProxy(true, false, ProxyAddress, "", "","");
        }

        public static bool SetProxy(string ProxyAddress, string ProxyExceptions)
        {
            return SetProxy(true, false, ProxyAddress, ProxyExceptions,"","");
        }
        
        public static bool SetAutoConfigURL(string AutoConfigURL)
        {
            return SetProxy(false, false, "", "", AutoConfigURL,"");
        }

        /// <summary>
        /// Sets proxy.
        /// </summary>
        /// <param name="EnableProxy">If true enables proxy, false disables.</param>
        /// <param name="EnableAutoDetect">If true enables Auto-Detect setting, false disables.</param>
        /// <param name="ProxyAddress">If you want a specific proxy server</param>
        /// <param name="ProxyExceptions">Exceptions if you need it</param>
        /// <param name="AutoConfigURL">If you want an autoconfig URL i.e. PAC file</param>
        /// <param name="ConnectionName">If you want to apply Proxy Setting to dial up connection, specify name here.</param>
        /// <returns></returns>
        public static bool SetProxy(bool EnableProxy,bool EnableAutoDetect,string ProxyAddress,string ProxyExceptions, string AutoConfigURL, string ConnectionName="")
        {
            // use this to store our proxy settings
            InternetPerConnOptionList list = new InternetPerConnOptionList();

            // get count of number of options we need...starting with a base of 2.
            int optionCount = 1;

            // count out how many options we are going to need to set...
            if (EnableProxy) optionCount++;
            if (EnableAutoDetect) optionCount++;
            if (!string.IsNullOrEmpty(ProxyExceptions)) optionCount++;
            if (!string.IsNullOrEmpty(AutoConfigURL)) optionCount++;

            // we'll use this array to store our options
            InternetConnectionOption[] options = new InternetConnectionOption[optionCount];

            // a counter to identify what option we are setting at the moment...there will always be option [0] at a minimum...
            int optionCurrent = 1;

            // our per connection flags get stored here...
            options[0].m_Option = PerConnOption.INTERNET_PER_CONN_FLAGS;

            // enable or disable proxy?
            if (EnableProxy)
            {
                options[1].m_Option = PerConnOption.INTERNET_PER_CONN_PROXY_SERVER;
                options[1].m_Value.m_StringPtr = Marshal.StringToHGlobalAuto(ProxyAddress);
                options[0].m_Value.m_Int = (int)PerConnFlags.PROXY_TYPE_PROXY;

                optionCurrent++;
            }
            else
            {
                options[0].m_Value.m_Int = (int)PerConnFlags.PROXY_TYPE_DIRECT;
            }
//            options[optionCurrent].m_Option = PerConnOption.INTERNET_PER_CONN_FLAGS;
  //          optionCurrent++;

            // configure proxy exceptions...use <local> to bypass proxy for local addresses...
            if (!string.IsNullOrEmpty(ProxyExceptions))
            {
                options[optionCurrent].m_Option = PerConnOption.INTERNET_PER_CONN_PROXY_BYPASS;
                options[optionCurrent].m_Value.m_StringPtr = Marshal.StringToHGlobalUni($"local;{ProxyExceptions}"); 
                optionCurrent++;
            }

            // configure auto config URLw
            if (!string.IsNullOrEmpty(AutoConfigURL))
            {
                options[optionCurrent].m_Option = PerConnOption.INTERNET_PER_CONN_AUTOCONFIG_URL;
                options[optionCurrent].m_Value.m_StringPtr = Marshal.StringToHGlobalUni(AutoConfigURL);
                options[0].m_Value.m_Int = (int)PerConnFlags.PROXY_TYPE_AUTO_PROXY_URL | (int)options[0].m_Value.m_Int;
                optionCurrent++;
            }

            if (EnableAutoDetect)
            {
                options[0].m_Value.m_Int = (int)PerConnFlags.PROXY_TYPE_AUTO_DETECT | (int)options[0].m_Value.m_Int;
                options[optionCurrent].m_Option = PerConnOption.INTERNET_PER_CONN_AUTODISCOVERY_FLAGS;
                options[optionCurrent].m_Value.m_Int=(int)AutoDetectFlags.AUTO_PROXY_FLAG_ALWAYS_DETECT;
                optionCurrent++;
            }

            // default stuff
            list.dwSize = Marshal.SizeOf(list);

            // do we have a connection name specified?
            if (string.IsNullOrEmpty(ConnectionName))
            {
                // no - the proxy will apply to 'LAN Connections'
                list.szConnection = IntPtr.Zero;
            }
            else
            {
                // yes - we will apply proxy to a specific connection
                list.szConnection = Marshal.StringToHGlobalAuto(ConnectionName);
            }

            list.dwOptionCount = options.Length;
            list.dwOptionError = 0;

            int optSize = Marshal.SizeOf(typeof(InternetConnectionOption));

            // make a pointer out of all that ...
            IntPtr optionsPtr = Marshal.AllocCoTaskMem(optSize * options.Length);

            // copy the array over into that spot in memory ...
            for (int i = 0; i < options.Length; ++i)
            {
                IntPtr opt = new IntPtr((long)(optionsPtr.ToInt64() + (i * optSize)));
                Marshal.StructureToPtr(options[i], opt, false);
            }

            list.options = optionsPtr;

            // and then make a pointer out of the whole list
            IntPtr ipcoListPtr = Marshal.AllocCoTaskMem((int)list.dwSize);
            Marshal.StructureToPtr(list, ipcoListPtr, false);

            // and finally, call the API method!
            int returnvalue = NativeMethods.InternetSetOption(IntPtr.Zero,
            InternetOption.INTERNET_OPTION_PER_CONNECTION_OPTION,
            ipcoListPtr, list.dwSize) ? -1 : 0;

            if (returnvalue == 0)
            {  // get the error codes, they might be helpful
                returnvalue = Marshal.GetLastWin32Error();
            }

            // FREE the data
            Marshal.FreeCoTaskMem(optionsPtr);
            Marshal.FreeCoTaskMem(ipcoListPtr);

            if (returnvalue > 0)
            {  // throw the error codes, they might be helpful (Most likely not!)
                throw new Win32Exception(returnvalue);
            }

            // refresh IE settings - so we don't need to re-launch internet explorer!
            NativeMethods.InternetSetOption(IntPtr.Zero, InternetOption.INTERNET_OPTION_PROXY_SETTINGS_CHANGED, IntPtr.Zero, 0);
            
            return (returnvalue < 0);
        }
    }
}