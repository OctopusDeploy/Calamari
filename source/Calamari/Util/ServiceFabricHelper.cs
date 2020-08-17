using System;
 using Microsoft.Win32;

 namespace Calamari.AzureServiceFabric.Util
{
    static class ServiceFabricHelper
    {
        public static bool IsServiceFabricSdkKeyInRegistry()
        {
            var keyFound = false;
            using (var rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var subKey = rootKey.OpenSubKey("SOFTWARE\\Microsoft\\Service Fabric SDK", false))
            {
                if (subKey != null)
                    keyFound = true;
            }
            return keyFound;
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
