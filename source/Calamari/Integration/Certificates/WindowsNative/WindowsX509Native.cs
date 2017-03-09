#if WINDOWS_CERTIFICATE_STORE_SUPPORT 
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Calamari.Integration.Certificates.WindowsNative
{
    internal static class WindowsX509Native
    {
        [DllImport("Crypt32.dll", SetLastError = true)]
        public static extern SafeCertStoreHandle CertOpenStore(CertStoreProviders lpszStoreProvider, IntPtr notUsed, IntPtr notUsed2, CertificateSystemStoreLocation location, [MarshalAs(UnmanagedType.LPWStr)]string storeName);

        [DllImport("Crypt32.dll", SetLastError = true)]
        public static extern bool CertCloseStore(IntPtr hCertStore, int dwFlags);

        [DllImport("Crypt32.dll", SetLastError = true)]
        public static extern SafeCertStoreHandle PFXImportCertStore(ref CryptoData pPfx, [MarshalAs(UnmanagedType.LPWStr)] string szPassword, PfxImportFlags dwFlags);

        [DllImport("Crypt32.dll", SetLastError = true)]
        public static extern bool CertAddCertificateContextToStore(SafeCertStoreHandle hCertStore, SafeCertContextHandle pCertContext, AddCertificateDisposition dwAddDisposition, ref IntPtr ppStoreContext);

        [DllImport("Crypt32.dll", SetLastError = true)]
        public static extern SafeCertContextHandle CertFindCertificateInStore(SafeCertStoreHandle hCertStore, CertificateEncodingType dwCertEncodingType, IntPtr notUsed, CertificateFindType dwFindType, ref CryptoData pvFindPara, IntPtr pPrevCertContext);

        [DllImport("Crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeCertContextHandle CertDuplicateCertificateContext(IntPtr pCertContext);

        [DllImport("Crypt32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CertGetCertificateContextProperty(IntPtr pCertContext, CertificateProperty dwPropId, [Out, MarshalAs(UnmanagedType.LPArray)] byte[] pvData, [In, Out] ref int pcbData);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CryptAcquireContextW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CryptAcquireContext(out IntPtr psafeProvHandle, 
            [MarshalAs(UnmanagedType.LPWStr)]string pszContainer, 
            [MarshalAs(UnmanagedType.LPWStr)]string pszProvider, 
            int dwProvType, CryptAcquireContextFlags dwFlags);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CryptSetProvParam(SafeCspHandle hProv, CspProperties dwParam, [In] byte[] pbData, SecurityDesciptorParts dwFlags);

        [DllImport("Crypt32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CryptAcquireCertificatePrivateKey(SafeCertContextHandle pCert,
                                                                      AcquireCertificateKeyOptions dwFlags,
                                                                      IntPtr pvReserved,        // void *
                                                                      [Out] out SafeCspHandle phCryptProvOrNCryptKey,
                                                                      [Out] out int dwKeySpec,
                                                                      [Out, MarshalAs(UnmanagedType.Bool)] out bool pfCallerFreeProvOrNCryptKey);

        [DllImport("Crypt32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CryptAcquireCertificatePrivateKey(SafeCertContextHandle pCert,
                                                                      AcquireCertificateKeyOptions dwFlags,
                                                                      IntPtr pvReserved,        // void *
                                                                      [Out] out SafeNCryptKeyHandle phCryptProvOrNCryptKey,
                                                                      [Out] out int dwKeySpec,
                                                                      [Out, MarshalAs(UnmanagedType.Bool)] out bool pfCallerFreeProvOrNCryptKey);

        [DllImport("Crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern
        bool CertEnumSystemStore(CertificateSystemStoreLocation dwFlags, IntPtr notUsed1, IntPtr notUsed2, CertEnumSystemStoreCallBackProto fn);

        /// <summary>
        /// signature of call back function used by CertEnumSystemStore
        /// </summary>
        internal delegate
        bool CertEnumSystemStoreCallBackProto([MarshalAs(UnmanagedType.LPWStr)] string storeName, uint dwFlagsNotUsed, IntPtr notUsed1, IntPtr notUsed2, IntPtr notUsed3);

        [DllImport("Ncrypt.dll", SetLastError = true, ExactSpelling = true)]
        internal static extern int NCryptSetProperty(SafeNCryptHandle hObject, [MarshalAs(UnmanagedType.LPWStr)] string szProperty, IntPtr pbInputByteArray, int cbInput, int flags);

        [DllImport("Ncrypt.dll")]
        internal static extern int NCryptDeleteKey(SafeNCryptKeyHandle hKey, int flags);

        [Flags]
        internal enum CertStoreProviders
        {
            CERT_STORE_PROV_SYSTEM = 10
        }

        internal enum AddCertificateDisposition
        {
            CERT_STORE_ADD_REPLACE_EXISTING = 3
        }

        internal enum CertificateSystemStoreLocation
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

        internal enum CertificateFindType
        {
            Sha1Hash = 1 << 16 // CERT_FIND_SHA1_HASH  
        }

        [Flags]
        internal enum CertificateEncodingType
        {
           X509AsnEncoding = 0x00000001, // X509_ASN_ENCODING
           Pkcs7AsnEncoding = 0x00010000, // PKCS_7_ASN_ENCODING
           Pkcs7OrX509AsnEncoding = X509AsnEncoding | Pkcs7AsnEncoding
        }

        // typedef struct _CRYPTOAPI_BLOB
        // {
        //      DWORD   cbData;
        //      BYTE    *pbData;
        // } CRYPT_HASH_BLOB, CRYPT_INTEGER_BLOB,
        //   CRYPT_OBJID_BLOB, CERT_NAME_BLOB;
        [StructLayout(LayoutKind.Sequential)]
        public struct CryptoData
        {
            public int cbData;
            public IntPtr pbData;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct KeyProviderInfo
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string pwszContainerName;

            [MarshalAs(UnmanagedType.LPWStr)]
            internal string pwszProvName;

            internal int dwProvType;

            internal int dwFlags;

            internal int cProvParam;

            internal IntPtr rgProvParam;        // PCRYPT_KEY_PROV_PARAM

            internal int dwKeySpec;
        }

        [Flags]
        public enum PfxImportFlags
        {
            CRYPT_EXPORTABLE = 0x00000001,
            CRYPT_MACHINE_KEYSET = 0x00000020,
            CRYPT_USER_KEYSET = 0x00001000,
            PKCS12_PREFER_CNG_KSP = 0x00000100,
            PKCS12_ALWAYS_CNG_KSP = 0x00000200
        }

        /// <summary>
        ///     Well known certificate property IDs
        /// </summary>
        public enum CertificateProperty
        {
            KeyProviderInfo = 2,    // CERT_KEY_PROV_INFO_PROP_ID 
            KeyContext = 5,    // CERT_KEY_CONTEXT_PROP_ID
        }

        /// <summary>
        ///     Flags for the CryptAcquireCertificatePrivateKey API
        /// </summary>
        [Flags]
        internal enum AcquireCertificateKeyOptions
        {
            None = 0x00000000,
            AcquireSilent = 0x00000040,
            AcquireAllowNCryptKeys = 0x00010000, // CRYPT_ACQUIRE_ALLOW_NCRYPT_KEY_FLAG
            AcquireOnlyNCryptKeys = 0x00040000,   // CRYPT_ACQUIRE_ONLY_NCRYPT_KEY_FLAG
        }

        [Flags]
        internal enum CryptAcquireContextFlags 
        {
            None = 0x00000000,
            Delete = 0x00000010, // CRYPT_DELETEKEYSET
            MachineKeySet = 0x00000020, // CRYPT_MACHINE_KEYSET
            Silent = 0x40 // CRYPT_SILENT
        }

        public enum CspProperties
        {
            SecurityDescriptor = 0x8 // PP_KEYSET_SEC_DESCR 
        }

        public static class NCryptProperties
        {
            public const string SecurityDescriptor = "Security Descr"; // NCRYPT_SECURITY_DESCR_PROPERTY 
        }

        [Flags]
        public enum NCryptFlags
        {
            Silent = 0x00000040,
        }

        public enum SecurityDesciptorParts
        {
            DACL_SECURITY_INFORMATION = 0x00000004
        }
    }
}
#endif