#if WINDOWS_CERTIFICATE_STORE_SUPPORT
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace Calamari.FullFrameworkTools.WindowsCertStore.WindowsNative
{
  internal class SafeCspHandle : SafeHandleZeroOrMinusOneIsInvalid
  {
    private SafeCspHandle()
      : base(true)
    {
    }

    [DllImport("advapi32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptContextAddRef(SafeCspHandle hProv, IntPtr pdwReserved, int dwFlags);

    [DllImport("advapi32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptReleaseContext(IntPtr hProv, int dwFlags);

    public SafeCspHandle Duplicate()
    {
      bool success = false;
      RuntimeHelpers.PrepareConstrainedRegions();
      try
      {
        this.DangerousAddRef(ref success);
        IntPtr handle = this.DangerousGetHandle();
        int hr = 0;
        SafeCspHandle safeCspHandle = new SafeCspHandle();
        RuntimeHelpers.PrepareConstrainedRegions();
        try
        {
        }
        finally
        {
          if (!SafeCspHandle.CryptContextAddRef(this, IntPtr.Zero, 0))
            hr = Marshal.GetLastWin32Error();
          else
            safeCspHandle.SetHandle(handle);
        }
        if (hr != 0)
        {
          safeCspHandle.Dispose();
          throw new CryptographicException(hr);
        }
        return safeCspHandle;
      }
      finally
      {
        if (success)
          this.DangerousRelease();
      }
    }

    protected override bool ReleaseHandle()
    {
      return SafeCspHandle.CryptReleaseContext(this.handle, 0);
    }
  }
}
#endif