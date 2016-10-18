using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Calamari.Integration.Certificates.WindowsNative
{
    internal static class WindowsX509Native
    {

        [DllImport("Crypt32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern SafeCertStoreHandle CertOpenStore(int storeProvider, uint dwMsgAndCertEncodingType, IntPtr hCryptProv, OpenStoreFlags dwFlags, string cchNameString);

        [DllImport("Crypt32.dll", SetLastError = true)]
        public static extern bool CertCloseStore(IntPtr hCertStore, Int32 dwFlags);

        [DllImport("Crypt32.dll", SetLastError = true)]
        public static extern SafeCertStoreHandle PFXImportCertStore(ref CryptoData pPfx, [MarshalAs(UnmanagedType.LPWStr)] string szPassword, PfxImportFlags dwFlags);

        [DllImport("Crypt32.DLL", SetLastError = true)]
        public static extern SafeCertContextHandle CertEnumCertificatesInStore(SafeCertStoreHandle storeProvider, IntPtr prevCertContext);

        [DllImport("Crypt32.dll", SetLastError = true)]
        public static extern bool CertAddCertificateContextToStore(SafeCertStoreHandle hCertStore, SafeCertContextHandle pCertContext, AddCertificateDisposition dwAddDisposition, ref IntPtr ppStoreContext);

        [DllImport("Crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeCertContextHandle CertDuplicateCertificateContext(IntPtr pCertContext);

        [DllImport("Crypt32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CertGetCertificateContextProperty(SafeCertContextHandle pCertContext, CertificateProperty dwPropId, [Out, MarshalAs(UnmanagedType.LPArray)] byte[] pvData, [In, Out] ref int pcbData);

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

        [DllImport("Ncrypt.dll", SetLastError = true, ExactSpelling = true)]
        internal static extern int NCryptSetProperty(SafeNCryptHandle hObject, [MarshalAs(UnmanagedType.LPWStr)] string szProperty, IntPtr pbInputByteArray, int cbInput, int flags);

        internal static class CertStoreProviders
        {
            public const int CERT_STORE_PROV_SYSTEM = 10;
        }

        [Flags]
        internal enum OpenStoreFlags
        {
            CERT_SYSTEM_STORE_CURRENT_USER = 1 << 16,
            CERT_SYSTEM_STORE_LOCAL_MACHINE = 2 << 16
        }

        internal enum AddCertificateDisposition
        {
            CERT_STORE_ADD_REPLACE_EXISTING = 3
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