
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Calamari.Integration.Certificates.WindowsNative
{
    internal static class SafeCertContextHandleExtensions
    {
        public static bool HasPrivateKey(this SafeCertContextHandle certificateContext)
        {
            return certificateContext.HasProperty(WindowsX509Native.CertificateProperty.KeyProviderInfo);
        }

        public static bool HasProperty(this SafeCertContextHandle certificateContext,
                                                    WindowsX509Native.CertificateProperty property)
        {
            byte[] buffer = null;
            var bufferSize = 0;
            // ReSharper disable once ExpressionIsAlwaysNull
            var hasProperty = WindowsX509Native.CertGetCertificateContextProperty(certificateContext, property, buffer, ref bufferSize);

            // ReSharper disable once InconsistentNaming
            const int ERROR_MORE_DATA = 0x000000ea;
            return hasProperty || Marshal.GetLastWin32Error() == ERROR_MORE_DATA;
        }

        /// <summary>
        ///     Get a property of a certificate formatted as a structure
        /// </summary>
        public static T GetCertificateProperty<T>(this SafeCertContextHandle certificateContext,
                                                    WindowsX509Native.CertificateProperty property) where T : struct
        {
            var rawProperty = GetCertificateProperty(certificateContext, property);

            var gcHandle = GCHandle.Alloc(rawProperty, GCHandleType.Pinned);
            var typedProperty = (T)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(T));
            gcHandle.Free();
            return typedProperty;
        }

        public static byte[] GetCertificateProperty(this SafeCertContextHandle certificateContext,
                                                      WindowsX509Native.CertificateProperty property)
        {
            byte[] buffer = null;
            var bufferSize = 0;
            // ReSharper disable once ExpressionIsAlwaysNull
            if (!WindowsX509Native.CertGetCertificateContextProperty(certificateContext, property, buffer, ref bufferSize))
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
            if (!WindowsX509Native.CertGetCertificateContextProperty(certificateContext, property, buffer, ref bufferSize))
            {
                throw new CryptographicException(Marshal.GetLastWin32Error());
            }

            return buffer;
        }
    }
}