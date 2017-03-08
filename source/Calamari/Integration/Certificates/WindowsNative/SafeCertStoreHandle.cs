#if WINDOWS_CERTIFICATE_STORE_SUPPORT 
using System;
using Microsoft.Win32.SafeHandles;

namespace Calamari.Integration.Certificates.WindowsNative
{
    internal class SafeCertStoreHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeCertStoreHandle() : base(true)
        {
        }

        private SafeCertStoreHandle(IntPtr handle) 
            : this(handle, true)
        {
        }

        public SafeCertStoreHandle(IntPtr handle, bool ownsHandle)
            : base(ownsHandle)
        {
           SetHandle(handle); 
        }

        protected override bool ReleaseHandle()
        {
            return WindowsX509Native.CertCloseStore(base.handle, 0);
        }

        public static SafeCertStoreHandle InvalidHandle => new SafeCertStoreHandle(IntPtr.Zero);
    }
}
#endif
