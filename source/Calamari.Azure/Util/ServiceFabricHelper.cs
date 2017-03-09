using Microsoft.Win32;

namespace Calamari.Azure.Util
{
    public static class ServiceFabricHelper
    {
        public static bool IsServiceFabricSDKKeyInRegistry()
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
}
