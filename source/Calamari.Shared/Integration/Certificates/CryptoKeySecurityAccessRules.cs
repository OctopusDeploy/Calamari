using System;
using System.Collections.Generic;
using Calamari.Integration.Certificates.WindowsNative;
#if WINDOWS_CERTIFICATE_STORE_SUPPORT
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using static Calamari.Integration.Certificates.WindowsNative.WindowsX509Native;
#endif

namespace Calamari.Integration.Certificates
{
#if WINDOWS_CERTIFICATE_STORE_SUPPORT
    public static class CryptoKeySecurityAccessRules
    {

        internal static void AddPrivateKeyAccessRules(ICollection<PrivateKeyAccessRule> accessRules, SafeCertContextHandle certificate)
        {
            try
            {
                var keyProvInfo = certificate.GetCertificateProperty<KeyProviderInfo>(CertificateProperty.KeyProviderInfo);

                // If it is a CNG key
                if (keyProvInfo.dwProvType == 0)
                {
                    SetCngPrivateKeySecurity(certificate, accessRules);
                }
                else
                {
                    SetCspPrivateKeySecurity(certificate, accessRules);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Could not set security on private-key", ex);
            }
        }

        static void SetCngPrivateKeySecurity(SafeCertContextHandle certificate, ICollection<PrivateKeyAccessRule> accessRules)
        {
            using (var key = CertificatePal.GetCngPrivateKey(certificate))
            {
                var security = GetCngPrivateKeySecurity(certificate);

                foreach (var cryptoKeyAccessRule in accessRules.Select(ToCryptoKeyAccessRule))
                {
                    security.AddAccessRule(cryptoKeyAccessRule);
                }

                var securityDescriptorBytes = security.GetSecurityDescriptorBinaryForm();
                var gcHandle = GCHandle.Alloc(securityDescriptorBytes, GCHandleType.Pinned);

                var errorCode = NCryptSetProperty(key,
                                                  WindowsX509Native.NCryptProperties.SecurityDescriptor,
                                                  gcHandle.AddrOfPinnedObject(),
                                                  securityDescriptorBytes.Length,
                                                  (int)WindowsX509Native.NCryptFlags.Silent | (int)WindowsX509Native.SecurityDesciptorParts.DACL_SECURITY_INFORMATION);

                gcHandle.Free();

                if (errorCode != 0)
                {
                    throw new CryptographicException(errorCode);
                }
            }
        }

        static void SetCspPrivateKeySecurity(SafeCertContextHandle certificate, ICollection<PrivateKeyAccessRule> accessRules)
        {
            using (var cspHandle = CertificatePal.GetCspPrivateKey(certificate))
            {
                var security = GetCspPrivateKeySecurity(certificate);

                foreach (var cryptoKeyAccessRule in accessRules.Select(ToCryptoKeyAccessRule))
                {
                    security.AddAccessRule(cryptoKeyAccessRule);
                }

                var securityDescriptorBytes = security.GetSecurityDescriptorBinaryForm();

                if (!CryptSetProvParam(cspHandle,
                                       WindowsX509Native.CspProperties.SecurityDescriptor,
                                       securityDescriptorBytes,
                                       WindowsX509Native.SecurityDesciptorParts.DACL_SECURITY_INFORMATION))
                {
                    throw new CryptographicException(Marshal.GetLastWin32Error());
                }
            }
        }

        static CryptoKeyAccessRule ToCryptoKeyAccessRule(PrivateKeyAccessRule privateKeyAccessRule)
        {
            switch (privateKeyAccessRule.Access)
            {
                case PrivateKeyAccess.ReadOnly:
                    return new CryptoKeyAccessRule(privateKeyAccessRule.GetIdentityReference(), CryptoKeyRights.GenericRead, AccessControlType.Allow);

                case PrivateKeyAccess.FullControl:
                    // We use 'GenericAll' here rather than 'FullControl' as 'FullControl' doesn't correctly set the access for CNG keys
                    return new CryptoKeyAccessRule(privateKeyAccessRule.GetIdentityReference(), CryptoKeyRights.GenericAll, AccessControlType.Allow);

                default:
                    throw new ArgumentOutOfRangeException(nameof(privateKeyAccessRule.Access));
            }
        }

        static CryptoKeySecurity GetCngPrivateKeySecurity(SafeCertContextHandle certificate)
        {
            using (var key = CertificatePal.GetCngPrivateKey(certificate))
            {
                var security = new CryptoKeySecurity();
                security.SetSecurityDescriptorBinaryForm(CertificatePal.GetCngPrivateKeySecurity(key),
                                                         AccessControlSections.Access);
                return security;
            }
        }

        static CryptoKeySecurity GetCspPrivateKeySecurity(SafeCertContextHandle certificate)
        {
            using (var cspHandle = CertificatePal.GetCspPrivateKey(certificate))
            {
                var security = new CryptoKeySecurity();
                security.SetSecurityDescriptorBinaryForm(CertificatePal.GetCspPrivateKeySecurity(cspHandle), AccessControlSections.Access);
                return security;
            }
        }


        public static CryptoKeySecurity GetPrivateKeySecurity(string thumbprint, StoreLocation storeLocation, string storeName)
        {
            var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);

            var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            store.Close();

            if (found.Count == 0)
                throw new Exception(
                                    $"Could not find certificate with thumbprint '{thumbprint}' in store Cert:\\{storeLocation}\\{storeName}");

            var certificate = new SafeCertContextHandle(found[0].Handle, false);

            if (!certificate.HasPrivateKey())
                throw new Exception("Certificate does not have a private-key");

            var keyProvInfo =
                certificate.GetCertificateProperty<WindowsX509Native.KeyProviderInfo>(WindowsX509Native.CertificateProperty.KeyProviderInfo);

            // If it is a CNG key
            return keyProvInfo.dwProvType == 0
                ? GetCngPrivateKeySecurity(certificate)
                : GetCspPrivateKeySecurity(certificate);
        }
    }
#else
    public static class CryptoKeySecurityAccessRules
    {
        internal static void AddPrivateKeyAccessRules(ICollection<PrivateKeyAccessRule> accessRules, SafeCertContextHandle certificate)
        {
            throw new NotImplementedException("Not Yet Available For NetCore");
        }
    }
#endif
}