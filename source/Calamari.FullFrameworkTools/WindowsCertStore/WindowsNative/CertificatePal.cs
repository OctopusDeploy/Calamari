using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using static Calamari.FullFrameworkTools.WindowsCertStore.WindowsNative.WindowsX509Native;
using Native = Calamari.FullFrameworkTools.WindowsCertStore.WindowsNative.WindowsX509Native;

namespace Calamari.FullFrameworkTools.WindowsCertStore.WindowsNative
{
    internal static class CertificatePal
    {
        public static bool HasProperty(IntPtr certificateContext, CertificateProperty property)
        {
            byte[]? buffer = null;
            var bufferSize = 0;
            // ReSharper disable once ExpressionIsAlwaysNull
            var hasProperty = CertGetCertificateContextProperty(certificateContext, property, buffer, ref bufferSize);

            // ReSharper disable once InconsistentNaming
            const int ERROR_MORE_DATA = 0x000000ea;
            return hasProperty || Marshal.GetLastWin32Error() == ERROR_MORE_DATA;
        }

        /// <summary>
        ///     Get a property of a certificate formatted as a structure
        /// </summary>
        public static T GetCertificateProperty<T>(IntPtr certificateContext, CertificateProperty property) where T : struct
        {
            var rawProperty = GetCertificateProperty(certificateContext, property);

            var gcHandle = GCHandle.Alloc(rawProperty, GCHandleType.Pinned);
            var typedProperty = (T)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(T));
            gcHandle.Free();
            return typedProperty;
        }

        public static byte[] GetCertificateProperty(IntPtr certificateContext, CertificateProperty property)
        {
            byte[]? buffer = null;
            var bufferSize = 0;
            // ReSharper disable once ExpressionIsAlwaysNull
            if (!CertGetCertificateContextProperty(certificateContext, property, buffer, ref bufferSize))
            {
                // ReSharper disable once InconsistentNaming
                const int ERROR_MORE_DATA = 0x000000ea;
                var errorCode = Marshal.GetLastWin32Error();

                if (errorCode != ERROR_MORE_DATA)
                {
                    throw new CryptographicException(errorCode);
                }
            }

            buffer = new byte[bufferSize];
            if (!CertGetCertificateContextProperty(certificateContext, property, buffer, ref bufferSize))
            {
                throw new CryptographicException(Marshal.GetLastWin32Error());
            }

            return buffer;
        }

        public static SafeCspHandle GetCspPrivateKey(SafeCertContextHandle certificate)
        {
            SafeCspHandle cspHandle;
            var keySpec = 0;
            var freeKey = true;
            if (!Native.CryptAcquireCertificatePrivateKey(certificate,
                Native.AcquireCertificateKeyOptions.AcquireSilent,
                IntPtr.Zero, out cspHandle, out keySpec, out freeKey))
            {
                throw new CryptographicException(Marshal.GetLastWin32Error());
            }

            if (cspHandle.IsInvalid)
                throw new Exception("Could not acquire private key");

            if (!freeKey)
            {
                var addedRef = false;
                cspHandle.DangerousAddRef(ref addedRef);
            }

            return cspHandle;
        }

        public static byte[] GetCspPrivateKeySecurity(SafeCspHandle cspHandle)
        {
            byte[]? buffer = null;
            var bufferSize = 0;

            // ReSharper disable once ExpressionIsAlwaysNull
            if (!Native.CryptGetProvParam(cspHandle, WindowsX509Native.CspProperties.SecurityDescriptor, buffer,
                    ref bufferSize, WindowsX509Native.SecurityDesciptorParts.DACL_SECURITY_INFORMATION))
            {
                // ReSharper disable once InconsistentNaming
                const int ERROR_MORE_DATA = 0x000000ea;
                var errorCode = Marshal.GetLastWin32Error();

                if (errorCode != ERROR_MORE_DATA)
                {
                    throw new CryptographicException(errorCode);
                }
            }

            buffer = new byte[bufferSize];
            if (!Native.CryptGetProvParam(cspHandle, WindowsX509Native.CspProperties.SecurityDescriptor, buffer,
                ref bufferSize, WindowsX509Native.SecurityDesciptorParts.DACL_SECURITY_INFORMATION))
            {
                throw new CryptographicException(Marshal.GetLastWin32Error());
            }

            return buffer;
        }

        public static byte[] GetCngPrivateKeySecurity(SafeNCryptKeyHandle hObject)
        {
            int bufferSize = 0;
            byte[]? buffer = null;

            var errorCode = Native.NCryptGetProperty(hObject, Native.NCryptProperties.SecurityDescriptor, null, 0,
                ref bufferSize,
                (int)Native.NCryptFlags.Silent |
                (int)Native.SecurityDesciptorParts.DACL_SECURITY_INFORMATION);

            if (errorCode != (int)Native.NCryptErrorCode.Success && errorCode != (int)Native.NCryptErrorCode.BufferTooSmall)
            {
                throw new CryptographicException(errorCode);
            }

            buffer = new byte[bufferSize];

            errorCode = Native.NCryptGetProperty(hObject, Native.NCryptProperties.SecurityDescriptor, buffer, bufferSize,
                ref bufferSize,
                (int)Native.NCryptFlags.Silent |
                (int)Native.SecurityDesciptorParts.DACL_SECURITY_INFORMATION);

            if (errorCode != (int)Native.NCryptErrorCode.Success)
            {
                throw new CryptographicException(errorCode);
            }

            return buffer;
        }

        public static SafeNCryptKeyHandle GetCngPrivateKey(SafeCertContextHandle certificate)
        {
            SafeNCryptKeyHandle key;
            int keySpec;
            var freeKey = true;

            if (!CryptAcquireCertificatePrivateKey(certificate,
                AcquireCertificateKeyOptions.AcquireOnlyNCryptKeys |
                AcquireCertificateKeyOptions.AcquireSilent,
                IntPtr.Zero, out key, out keySpec, out freeKey))
            {
                throw new CryptographicException(Marshal.GetLastWin32Error());
            }

            if (key.IsInvalid)
                throw new Exception("Could not acquire provide key");

            if (!freeKey)
            {
                var addedRef = false;
                key.DangerousAddRef(ref addedRef);
            }

            return key;
        }

        public static void DeleteCngKey(SafeNCryptKeyHandle key)
        {
            var errorCode = NCryptDeleteKey(key, 0);

            if (errorCode != 0)
                throw new CryptographicException(errorCode);
        }

        public static string GetSubjectName(SafeCertContextHandle certificate)
        {
            var flags = CertNameFlags.None;
            var stringType = CertNameStringType.CERT_X500_NAME_STR | CertNameStringType.CERT_NAME_STR_REVERSE_FLAG;

            var cchCount = CertGetNameString(certificate, CertNameType.CERT_NAME_RDN_TYPE, flags, ref stringType, null, 0);
            if (cchCount == 0)
                throw new CryptographicException(Marshal.GetHRForLastWin32Error());

            var sb = new StringBuilder(cchCount);
            CertGetNameString(certificate, CertNameType.CERT_NAME_RDN_TYPE, flags, ref stringType, sb, cchCount);

            return sb.ToString();
        }
    }
}