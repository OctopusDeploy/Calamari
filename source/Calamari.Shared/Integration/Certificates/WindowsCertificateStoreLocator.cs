using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Calamari.Integration.Certificates
{
    public static class WindowsCertificateStoreLocator
    {
        static readonly IList<string> EnumeratedStoreNames = new List<string>();
        static readonly object StoreNamesSyncLock = new object();
        
        public static string? FindCertificateStore(string thumbprint, StoreLocation storeLocation)
        {
            foreach (var storeName in GetStoreNames(storeLocation))
            {
                var store = new X509Store(storeName, storeLocation);
                store.Open(OpenFlags.ReadOnly);

                var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                if (found.Count != 0 && found[0].HasPrivateKey)
                {
                    return storeName;
                }

                store.Close();
            }

            return null;
        }
        
        public static ICollection<string> GetStoreNames(StoreLocation location)
        {
            lock (StoreNamesSyncLock)
            {
                var callback = new CertEnumSystemStoreCallBackProto(CertEnumSystemStoreCallBack);
                var names = new List<string>();
                CertificateSystemStoreLocation locationFlags;

                switch (location)
                {
                    case StoreLocation.CurrentUser:
                        locationFlags = CertificateSystemStoreLocation.CurrentUser;
                        break;
                    case StoreLocation.LocalMachine:
                        locationFlags = CertificateSystemStoreLocation.LocalMachine;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(location), location, null);
                }

                EnumeratedStoreNames.Clear();
                CertEnumSystemStore(locationFlags, IntPtr.Zero, IntPtr.Zero, callback);
                names.AddRange(EnumeratedStoreNames);

                return names;
            }
        }
        
        /// <summary>
        /// call back function used by CertEnumSystemStore
        ///
        /// Currently, there is no managed support for enumerating store
        /// names on a machine. We use the win32 function CertEnumSystemStore()
        /// to get a list of stores for a given context.
        ///
        /// Each time this callback is called, we add the passed store name
        /// to the list of stores
        /// </summary>
        static bool CertEnumSystemStoreCallBack(string storeName, uint dwFlagsNotUsed, IntPtr notUsed1, IntPtr notUsed2, IntPtr notUsed3)
        {
            EnumeratedStoreNames.Add(storeName);
            return true;
        }
        
        [DllImport("Crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern
            bool CertEnumSystemStore(CertificateSystemStoreLocation dwFlags, IntPtr notUsed1, IntPtr notUsed2,
                                     CertEnumSystemStoreCallBackProto fn);
        
        enum CertificateSystemStoreLocation
        {
            CurrentUser = 1 << 16, // CERT_SYSTEM_STORE_CURRENT_USER
            LocalMachine = 2 << 16, // CERT_SYSTEM_STORE_LOCAL_MACHINE
            CurrentService = 4 << 16, // CERT_SYSTEM_STORE_CURRENT_SERVICE
            Services = 5 << 16, // CERT_SYSTEM_STORE_SERVICES
            Users = 6 << 16, // CERT_SYSTEM_STORE_USERS
            UserGroupPolicy = 7 << 16, // CERT_SYSTEM_STORE_CURRENT_USER_GROUP_POLICY
            MachineGroupPolicy = 8 << 16, // CERT_SYSTEM_STORE_LOCAL_MACHINE_GROUP_POLICY
            LocalMachineEnterprise = 9 << 16, // CERT_SYSTEM_STORE_LOCAL_MACHINE_ENTERPRISE
        }
        
        /// <summary>
        /// signature of call back function used by CertEnumSystemStore
        /// </summary>
        delegate
            bool CertEnumSystemStoreCallBackProto(
                [MarshalAs(UnmanagedType.LPWStr)] string storeName, uint dwFlagsNotUsed, IntPtr notUsed1,
                IntPtr notUsed2, IntPtr notUsed3);
    }
}