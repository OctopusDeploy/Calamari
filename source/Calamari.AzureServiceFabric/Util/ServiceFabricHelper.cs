using System;

namespace Calamari.AzureServiceFabric.Util
{
    static class ServiceFabricHelper
    {
        public static bool IsServiceFabricSdkInstalled()
        {
            var isInstalled = false;
#if NETFRAMEWORK
            using (var rootKey =  Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64))
            using (var subKey = rootKey.OpenSubKey(@"SOFTWARE\Microsoft\Service Fabric SDK", false))
            {
                if (subKey != null)
                    isInstalled = true;
            }
#else
            var programFiles = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%");
            var sdkFolderPath = System.IO.Path.Combine("Microsoft SDKs", "Service Fabric");
            
            //x86 first
            isInstalled = System.IO.Directory.Exists(System.IO.Path.Combine(programFiles, sdkFolderPath));

            //we first look in the x86 path, if it's not there, we check in the x64 program files
            if (!isInstalled)
            {
                // On 32-bit Operating Systems, this will return C:\Program Files
                // On 64-bit Operating Systems - regardless of process bitness, this will return C:\Program Files
                if (!Environment.Is64BitOperatingSystem || Environment.Is64BitProcess)
                {
                    programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                }
                else
                {
                    // 32 bit process on a 64 bit OS can't use SpecialFolder.ProgramFiles to get the 64-bit program files folder
                    programFiles = Environment.ExpandEnvironmentVariables("%ProgramW6432%");
                }
                
                //check to see if the directory exists again in the new program files
                isInstalled = System.IO.Directory.Exists(System.IO.Path.Combine(programFiles, sdkFolderPath));
            }

#endif
            return isInstalled;
        }
    }

    enum AzureServiceFabricSecurityMode
    {
        Unsecure,
        SecureClientCertificate,
        SecureAzureAD,
        SecureAD,
    }
}