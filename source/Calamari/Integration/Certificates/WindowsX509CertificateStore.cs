#if WINDOWS_CERTIFICATE_STORE_SUPPORT 
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using Calamari.Integration.Certificates.WindowsNative;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;
using static Calamari.Integration.Certificates.WindowsNative.WindowsX509Native;
using Native = Calamari.Integration.Certificates.WindowsNative.WindowsX509Native;

namespace Calamari.Integration.Certificates
{
    public class WindowsX509CertificateStore
    {
        public static void ImportCertificateToStore(byte[] pfxBytes, string password, StoreLocation storeLocation,
            string storeName, bool privateKeyExportable)
        {
            CertificateSystemStoreLocations systemStoreLocation;
            bool useUserKeyStore;

            switch (storeLocation)
            {
                case StoreLocation.CurrentUser:
                    systemStoreLocation = CertificateSystemStoreLocations.CurrentUser;
                    useUserKeyStore = true;
                    break;
                case StoreLocation.LocalMachine:
                    systemStoreLocation = CertificateSystemStoreLocations.LocalMachine;
                    useUserKeyStore = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(storeLocation), storeLocation, null);
            }

            using (var store = Native.CertOpenStore(CertStoreProviders.CERT_STORE_PROV_SYSTEM, IntPtr.Zero, IntPtr.Zero,
                systemStoreLocation, storeName))
            {
                ImportPfxToStore(store, pfxBytes, password, useUserKeyStore, privateKeyExportable);
            }
        }

        /// <summary>
        /// Import a certificate into a specific user's store 
        /// </summary>
        public static void ImportCertificateToStore(byte[] pfxBytes, string password, string userName,
            string storeName, bool privateKeyExportable)
        {
            var account = new NTAccount(userName);
            var sid = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
            var userStoreName = sid + "\\" + storeName;

            using (var store = Native.CertOpenStore(CertStoreProviders.CERT_STORE_PROV_SYSTEM, IntPtr.Zero, IntPtr.Zero,
                CertificateSystemStoreLocations.Users, userStoreName))
            {
                // Note we use the machine key-store. There is no way to store the private-key in 
                // another user's key-store.  
                var certificate = ImportPfxToStore(store, pfxBytes, password, false, privateKeyExportable);

                // Because we have to store the private-key in the machine key-store, we must grant the user access to it
                var keySecurity =
                    PrivateKeyAccessRule.CreateCryptoKeySecurity(new [] { new PrivateKeyAccessRule(account, PrivateKeyAccess.FullControl) }); 
                SetPrivateKeySecurity(keySecurity, certificate);
            }
        }

        public static void SetPrivateKeySecurity(string thumbprint, StoreLocation storeLocation, string storeName, 
            ICollection<PrivateKeyAccessRule> privateKeyAccessRules)
        {
            var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadWrite);

            var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);

            if (found.Count == 0)
                throw new Exception($"Could not find certificate with thumbprint '{thumbprint}' in store Cert:\\{storeLocation}\\{storeName}");

            var certificate = new SafeCertContextHandle(found[0].Handle, false);

            if (!certificate.HasPrivateKey())
                throw new Exception("Certificate does not have a private-key");

            SetPrivateKeySecurity(PrivateKeyAccessRule.CreateCryptoKeySecurity(privateKeyAccessRules), certificate);

            store.Close();
        }

        /// <summary>
        /// Unlike X509Store.Remove() this function also cleans up private-keys
        /// </summary>
        public static void RemoveCertificateFromStore(string thumbprint, StoreLocation storeLocation, string storeName)
        {
            var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadWrite);

            var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);

            if (found.Count == 0)
                return;

            var certificate = found[0];
            var certificateHandle = new SafeCertContextHandle(found[0].Handle, false);

            // If the certificate has a private-key, remove it
            if (certificateHandle.HasPrivateKey())
            {
                var keyProvInfo = certificateHandle.GetCertificateProperty<KeyProviderInfo>(CertificateProperty.KeyProviderInfo);

                // If it is a CNG key
                if (keyProvInfo.dwProvType == 0)
                {
                    try
                    {
                        var key = CertificatePal.GetCngPrivateKey(certificateHandle);
                        CertificatePal.DeleteCngKey(key);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Exception while deleting CNG private key", ex);
                    }
                }
                else // CAPI key
                {
                    try
                    {
                        IntPtr providerHandle;
                        var acquireContextFlags = CryptAcquireContextFlags.Delete | CryptAcquireContextFlags.Silent;
                        if (storeLocation == StoreLocation.LocalMachine)
                            acquireContextFlags = acquireContextFlags | CryptAcquireContextFlags.MachineKeySet;

                        var success = Native.CryptAcquireContext(out providerHandle, keyProvInfo.pwszContainerName,
                            keyProvInfo.pwszProvName,
                            keyProvInfo.dwProvType, acquireContextFlags);

                        if (!success)
                            throw new CryptographicException(Marshal.GetLastWin32Error());
                    }
                    catch (Exception ex)
                    {
                        // Swallow keyset does not exist
                        if (!(ex is CryptographicException && ex.Message.Contains("Keyset does not exist")))
                        {
                            throw new Exception("Exception while deleting CAPI private key", ex);
                        }
                    }
                }
            }

            store.Remove(certificate);
            store.Close();
        }

        public static ICollection<string> GetStoreNames(StoreLocation location)
        {
            var callback = new CertEnumSystemStoreCallBackProto(CertEnumSystemStoreCallBack); 
            var names = new List<string>();
            CertificateSystemStoreLocations locationFlags;

            switch (location)
            {
                case StoreLocation.CurrentUser:
                    locationFlags = CertificateSystemStoreLocations.CurrentUser; 
                    break;
                case StoreLocation.LocalMachine:
                    locationFlags = CertificateSystemStoreLocations.LocalMachine; 
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(location), location, null);
            }

            lock (StoreNamesSyncLock)
            {
                EnumeratedStoreNames.Clear();
                CertEnumSystemStore(locationFlags, IntPtr.Zero, IntPtr.Zero, callback);
                names.AddRange(EnumeratedStoreNames);
            }

            return names;
        }

        static SafeCertContextHandle ImportPfxToStore(SafeCertStoreHandle store, byte[] pfxBytes, string password,
            bool useUserKeyStore, bool privateKeyExportable)
        {
            var pfxImportFlags = useUserKeyStore
                ? PfxImportFlags.CRYPT_USER_KEYSET
                : PfxImportFlags.CRYPT_MACHINE_KEYSET;

            if (privateKeyExportable)
            {
                pfxImportFlags = pfxImportFlags | PfxImportFlags.CRYPT_EXPORTABLE;
            }

            var certificates = GetCertificatesFromPfx(pfxBytes, password, pfxImportFlags);

            foreach (var certificate in certificates)
            {
                AddCertificateToStore(store, certificate);
            }

            return certificates.First();
        }

        private static readonly IList<string> EnumeratedStoreNames  = new List<string>();
        private static readonly object StoreNamesSyncLock = new object(); 

        /// <summary>
        /// call back function used by CertEnumSystemStore
        ///
        /// Currently, there is no managed support for enumerating store
        /// names on a machine. We use the win32 function CertEnumSystemStore()
        /// to get a list of stores for a given context.
        ///
        /// Each time this callback is called, we add the passed store name
        /// to the list of stores
        /// </summary>
        internal static bool CertEnumSystemStoreCallBack(string storeName, uint dwFlagsNotUsed, IntPtr notUsed1, IntPtr notUsed2, IntPtr notUsed3)
        {
            EnumeratedStoreNames.Add(storeName);
            return true;
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

        static IList<SafeCertContextHandle> GetCertificatesFromPfx(byte[] pfxBytes, string password, PfxImportFlags pfxImportFlags)
        {
            // Marshal PFX bytes into native data structure
            var pfxData = new CryptoData
            {
                cbData = pfxBytes.Length,
                pbData = Marshal.AllocHGlobal(pfxBytes.Length)
            };

            Marshal.Copy(pfxBytes, 0, pfxData.pbData, pfxBytes.Length);

            var certificates = new List<SafeCertContextHandle>();

            try
            {
                using (var memoryStore = PFXImportCertStore(ref pfxData, password, pfxImportFlags))
                {
                    if (memoryStore.IsInvalid)
                        throw new CryptographicException(Marshal.GetLastWin32Error());

                    var certificatesToImport = GetCertificatesToImport(pfxBytes, password);

                    foreach (var certificate in certificatesToImport)
                    {
                        var thumbprint = CalculateThumbprint(certificate);
                        // Marshal PFX bytes into native data structure
                        var thumbprintData = new CryptoData
                        {
                            cbData = thumbprint.Length,
                            pbData = Marshal.AllocHGlobal(thumbprint.Length)
                        };

                        Marshal.Copy(thumbprint, 0, thumbprintData.pbData, thumbprint.Length);

                        certificates.Add(CertFindCertificateInStore(memoryStore,
                            CertificateEncodingType.Pkcs7OrX509AsnEncoding,
                            IntPtr.Zero, IntPtr.Zero, CertificateFindType.Sha1Hash, ref thumbprintData, IntPtr.Zero));

                        Marshal.FreeHGlobal(thumbprintData.pbData);
                    }

                    return certificates;
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
            using (var key = CertificatePal.GetCngPrivateKey(certificate))
            {
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

        static IList<Org.BouncyCastle.X509.X509Certificate> GetCertificatesToImport(byte[] pfxBytes, string password)
        {
            using (var memoryStream = new MemoryStream(pfxBytes))
            {
               var pkcs12Store = new Pkcs12Store(memoryStream, password?.ToCharArray()); 

                if (pkcs12Store.Count < 1)
                    throw new Exception("No certificates were found in PFX");

               var aliases = pkcs12Store.Aliases.Cast<string>().ToList();

                // Find the first bag which contains a private-key
                var keyAlias = aliases.FirstOrDefault(alias => pkcs12Store.IsKeyEntry(alias));

                if (keyAlias != null)
                {
                    return pkcs12Store.GetCertificateChain(keyAlias).Select(x => x.Certificate).ToList();
                }

                return new List<Org.BouncyCastle.X509.X509Certificate> {pkcs12Store.GetCertificate(aliases.First()).Certificate};
            }
        }

        static byte[] CalculateThumbprint(Org.BouncyCastle.X509.X509Certificate certificate)
        {
            var der = certificate.GetEncoded();
            return DigestUtilities.CalculateDigest("SHA1", der);
        }
    }
}
#endif