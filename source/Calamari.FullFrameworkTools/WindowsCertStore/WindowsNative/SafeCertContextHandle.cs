#if WINDOWS_CERTIFICATE_STORE_SUPPORT
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Calamari.FullFrameworkTools.WindowsCertStore.WindowsNative
{
    /// <summary>
    ///     <para>
    ///         SafeCertContextHandle provides a SafeHandle class for an X509Certificate's certificate context
    ///         as stored in its <see cref="System.Security.Cryptography.X509Certificates.X509Certificate.Handle" />
    ///         property.  This can be used instead of the raw IntPtr to avoid races with the garbage
    ///         collector, ensuring that the X509Certificate object is not cleaned up from underneath you
    ///         while you are still using the handle pointer.
    ///     </para>
    ///     <para>
    ///         This safe handle type represents a native CERT_CONTEXT.
    ///         (http://msdn.microsoft.com/en-us/library/aa377189.aspx)
    ///     </para>
    ///     <para>
    ///         A SafeCertificateContextHandle for an X509Certificate can be obtained by calling the <see
    ///         cref="X509CertificateExtensionMethods.GetCertificateContext" /> extension method.
    ///     </para>
    /// </summary>
    internal sealed class SafeCertContextHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeCertContextHandle() : base(true)
        {
        }

        public SafeCertContextHandle(IntPtr handle, bool ownsHandle)
            : base(false)
        {
            SetHandle(handle);
        }

        [DllImport("crypt32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CertFreeCertificateContext(IntPtr pCertContext);

        public WindowsX509Native.CERT_CONTEXT CertificateContext => (WindowsX509Native.CERT_CONTEXT)Marshal.PtrToStructure(handle, typeof(WindowsX509Native.CERT_CONTEXT));

        protected override bool ReleaseHandle()
        {
            return CertFreeCertificateContext(handle);
        }

        public SafeCertContextHandle Duplicate()
        {
            return WindowsX509Native.CertDuplicateCertificateContext(this.DangerousGetHandle());
        }

        public IntPtr Disconnect()
        {
            var ptr = DangerousGetHandle();
            SetHandle(IntPtr.Zero);
            return ptr;
        }
    }
}
#endif