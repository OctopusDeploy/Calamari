using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Calamari.Integration.Certificates.WindowsNative;
using Microsoft.Win32.SafeHandles;
using static Calamari.Integration.Certificates.WindowsNative.WindowsX509Native;
using Native = Calamari.Integration.Certificates.WindowsNative.WindowsX509Native;

namespace Calamari.Integration.Certificates
{
    public class WindowsX509CertificateStore
    {
        public static void ImportCertificateToStore(byte[] pfxBytes, string password, StoreLocation storeLocation,
            string storeName, bool privateKeyExportable, ICollection<PrivateKeyAccessRule> privateKeyAccessRules)
        {
            using (var store = OpenCertStore(storeLocation, storeName))
            {
                var pfxImportFlags = storeLocation == StoreLocation.LocalMachine 
                    ?  PfxImportFlags.CRYPT_MACHINE_KEYSET
                    :  PfxImportFlags.CRYPT_USER_KEYSET;

                if (privateKeyExportable)
                {
                    pfxImportFlags = pfxImportFlags | PfxImportFlags.CRYPT_EXPORTABLE;
                }

                var certificate = GetCertificateFromPfx(pfxBytes, password, pfxImportFlags);

                AddCertificateToStore(store, certificate);

                if (certificate.HasPrivateKey())
                {
                    SetPrivateKeySecurity(PrivateKeyAccessRule.CreateCryptoKeySecurity(privateKeyAccessRules), certificate);
                }
            }
        }

        static SafeCertStoreHandle OpenCertStore(StoreLocation location, string storeName)
        {
            try
            {
                var certStoreHandle =
                    CertOpenStore(CertStoreProviders.CERT_STORE_PROV_SYSTEM, 0,
                        IntPtr.Zero,
                        location == StoreLocation.CurrentUser
                            ? OpenStoreFlags.CERT_SYSTEM_STORE_CURRENT_USER
                            : OpenStoreFlags.CERT_SYSTEM_STORE_LOCAL_MACHINE,
                        storeName
                    );

                if (certStoreHandle.IsInvalid)
                    throw new CryptographicException(Marshal.GetLastWin32Error());

                return certStoreHandle;
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not open certificate store {storeName} at location {location}", ex);
            }
        }

        static void AddCertificateToStore(SafeCertStoreHandle store, SafeCertContextHandle certificate)
        {
            try
            {
                var storeContext = IntPtr.Zero;
                if (!CertAddCertificateContextToStore(store, certificate,
                    AddCertificateDisposition.CERT_STORE_ADD_REPLACE_EXISTING, ref storeContext))
                {
                    throw new CryptographicException(Marshal.GetLastWin32Error());
                }
            }
            catch (Exception ex)
            {
               throw new Exception("Could not add certificate to store", ex); 
            }
        }

        static SafeCertContextHandle GetCertificateFromPfx(byte[] pfxBytes, string password, PfxImportFlags pfxImportFlags)
        {
            // Marshal PFX bytes into native data structure
            var pfxData = new CryptoData
            {
                cbData = pfxBytes.Length,
                pbData = Marshal.AllocHGlobal(pfxBytes.Length)
            };

            Marshal.Copy(pfxBytes, 0, pfxData.pbData, pfxBytes.Length);
            try
            {
                using (var memoryStore = PFXImportCertStore(ref pfxData, password, pfxImportFlags))
                {
                    if (memoryStore.IsInvalid)
                        throw new CryptographicException(Marshal.GetLastWin32Error());

                    // Find the first certificate with a private-key.
                    // If no certs have a private-key, then return the first certificate.
                    // TODO: should really clean-up the key-containers of any certs we don't return
                    SafeCertContextHandle chosenCertificate = null;
                    SafeCertContextHandle currentCertificate = null;

                    while (EnumerateCertificatesInStore(memoryStore, ref currentCertificate))
                    {
                        // If the certificate has a private-key
                        if (currentCertificate.HasPrivateKey())
                        {
                            if (chosenCertificate != null || chosenCertificate.IsInvalid || !chosenCertificate.HasPrivateKey())
                            {
                                return currentCertificate.Duplicate();
                            }
                        }
                        else
                        {
                            // Doesn't have a private key but hang on to it anyway in case we don't find any certs with a private key.
                            if (chosenCertificate == null)
                                chosenCertificate = currentCertificate.Duplicate();
                        }
                    }

                    if (chosenCertificate == null || chosenCertificate.IsInvalid)
                        throw new Exception("Did not find certificate in PFX");

                    return chosenCertificate;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Could not read PFX", ex);
            }
            finally
            {
                Marshal.FreeHGlobal(pfxData.pbData);
            }
        }

        static void SetPrivateKeySecurity(CryptoKeySecurity privateKeySecurity, SafeCertContextHandle certificate)
        {
            try
            {
                var keyProvInfo = certificate.GetCertificateProperty<KeyProviderInfo>(
                    CertificateProperty.KeyProviderInfo);

                // If it is a CNG key
                if (keyProvInfo.dwProvType == 0)
                {
                    SetCngPrivateKeySecurity(certificate, privateKeySecurity);
                }
                else
                {
                    SetCspPrivateKeySecurity(certificate, privateKeySecurity);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Could not set security on private-key", ex);
            }
        }

        static void SetCngPrivateKeySecurity(SafeCertContextHandle certificate, CryptoKeySecurity security)
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

            using (key)
            {
                if (!freeKey)
                {
                    var addedRef = false;
                    key.DangerousAddRef(ref addedRef);
                }

                var securityDescriptorBytes = security.GetSecurityDescriptorBinaryForm();
                var gcHandle = GCHandle.Alloc(securityDescriptorBytes, GCHandleType.Pinned);

                var errorCode = NCryptSetProperty(key,
                    NCryptProperties.SecurityDescriptor,
                    gcHandle.AddrOfPinnedObject(), securityDescriptorBytes.Length,
                    (int)NCryptFlags.Silent |
                    (int)SecurityDesciptorParts.DACL_SECURITY_INFORMATION);

                gcHandle.Free();

                if (errorCode != 0)
                {
                    throw new CryptographicException(errorCode);
                }
            }
        }

        static void SetCspPrivateKeySecurity(SafeCertContextHandle certificate, CryptoKeySecurity security)
        {
            SafeCspHandle cspHandle;
            var keySpec = 0;
            var freeKey = true;
            if (!CryptAcquireCertificatePrivateKey(certificate,
                AcquireCertificateKeyOptions.AcquireSilent,
                IntPtr.Zero, out cspHandle, out keySpec, out freeKey))
            {
                throw new CryptographicException(Marshal.GetLastWin32Error());
            }

            if (cspHandle.IsInvalid)
                throw new Exception("Could not acquire private key");

            using (cspHandle)
            {
                if (!freeKey)
                {
                    var addedRef = false;
                    cspHandle.DangerousAddRef(ref addedRef);
                }

                var securityDescriptorBytes = security.GetSecurityDescriptorBinaryForm();

                if (!CryptSetProvParam(cspHandle, CspProperties.SecurityDescriptor,
                    securityDescriptorBytes, SecurityDesciptorParts.DACL_SECURITY_INFORMATION))
                {
                    throw new CryptographicException(Marshal.GetLastWin32Error());
                }
            }
        }

        static bool EnumerateCertificatesInStore(SafeCertStoreHandle store, ref SafeCertContextHandle currentCertificate)
        {
            var previousCertificate = currentCertificate?.Disconnect() ?? IntPtr.Zero;
            currentCertificate = CertEnumCertificatesInStore(store, previousCertificate);
            return !currentCertificate.IsInvalid;
        }



    }
}