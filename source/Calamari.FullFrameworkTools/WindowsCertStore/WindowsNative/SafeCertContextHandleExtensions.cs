using System;

#if WINDOWS_CERTIFICATE_STORE_SUPPORT 

namespace Calamari.FullFrameworkTools.WindowsCertStore.WindowsNative
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
            return CertificatePal.HasProperty(certificateContext.DangerousGetHandle(), property);
        }

        /// <summary>
        ///     Get a property of a certificate formatted as a structure
        /// </summary>
        public static T GetCertificateProperty<T>(this SafeCertContextHandle certificateContext,
                                                    WindowsX509Native.CertificateProperty property) where T : struct
        {
            return CertificatePal.GetCertificateProperty<T>(certificateContext.DangerousGetHandle(), property);
        }
    }
}
#endif