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
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Certificates.WindowsNative;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using static Calamari.Integration.Certificates.WindowsNative.WindowsX509Native;
using Native = Calamari.Integration.Certificates.WindowsNative.WindowsX509Native;

namespace Calamari.Integration.Certificates
{
    public class WindowsX509CertificateStore
    {
        public static readonly ISemaphoreFactory Semaphores = SemaphoreFactory.Get();
        public static readonly string SemaphoreName = nameof(WindowsX509CertificateStore);

        const string IntermediateAuthorityStoreName = "CA";
        public static readonly string RootAuthorityStoreName = "Root";

        private static IDisposable AcquireSemaphore()
        {
            return Semaphores.Acquire(SemaphoreName, "Another process is working with the certificate store, please wait...");
        }

        public static void ImportCertificateToStore(byte[] pfxBytes, string password, StoreLocation storeLocation,
            string storeName, bool privateKeyExportable)
        {
            using (AcquireSemaphore())
            {
                CertificateSystemStoreLocation systemStoreLocation;
                bool useUserKeyStore;

                switch (storeLocation)
                {
                    case StoreLocation.CurrentUser:
                        systemStoreLocation = CertificateSystemStoreLocation.CurrentUser;
                        useUserKeyStore = true;
                        break;
                    case StoreLocation.LocalMachine:
                        systemStoreLocation = CertificateSystemStoreLocation.LocalMachine;
                        useUserKeyStore = false;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(storeLocation), storeLocation, null);
                }

                ImportPfxToStore(systemStoreLocation, storeName, pfxBytes, password, useUserKeyStore,
                    privateKeyExportable);
            }
        }

        /// <summary>
        /// Import a certificate into a specific user's store 
        /// </summary>
        public static void ImportCertificateToStore(byte[] pfxBytes, string password, string userName,
            string storeName, bool privateKeyExportable)
        {
            using (AcquireSemaphore())
            {
                var account = new NTAccount(userName);
                var sid = (SecurityIdentifier) account.Translate(typeof(SecurityIdentifier));
                var userStoreName = sid + "\\" + storeName;

                // Note we use the machine key-store. There is no way to store the private-key in 
                // another user's key-store.  
                var certificate = ImportPfxToStore(CertificateSystemStoreLocation.Users, userStoreName, pfxBytes,
                    password, false, privateKeyExportable);

                if (certificate.HasPrivateKey())
                {
                    // Because we have to store the private-key in the machine key-store, we must grant the user access to it
                    var keySecurity = new[] {new PrivateKeyAccessRule(account, PrivateKeyAccess.FullControl)};
                    AddPrivateKeyAccessRules(keySecurity, certificate);
                }
            }
        }

        public static void AddPrivateKeyAccessRules(string thumbprint, StoreLocation storeLocation, string storeName,
            ICollection<PrivateKeyAccessRule> privateKeyAccessRules)
        {
            using (AcquireSemaphore())
            {
                var store = new X509Store(storeName, storeLocation);
                store.Open(OpenFlags.ReadWrite);

                var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);

                if (found.Count == 0)
                    throw new Exception(
                        $"Could not find certificate with thumbprint '{thumbprint}' in store Cert:\\{storeLocation}\\{storeName}");

                var certificate = new SafeCertContextHandle(found[0].Handle, false);

                if (!certificate.HasPrivateKey())
                    throw new Exception("Certificate does not have a private-key");

                AddPrivateKeyAccessRules(privateKeyAccessRules, certificate);

                store.Close();
            }
        }

        public static CryptoKeySecurity GetPrivateKeySecurity(string thumbprint, StoreLocation storeLocation, string storeName)
        {
            using (AcquireSemaphore())
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
                    certificate.GetCertificateProperty<KeyProviderInfo>(CertificateProperty.KeyProviderInfo);

                // If it is a CNG key
                return keyProvInfo.dwProvType == 0
                    ? GetCngPrivateKeySecurity(certificate)
                    : GetCspPrivateKeySecurity(certificate);
            }
        }

        /// <summary>
        /// Unlike X509Store.Remove() this function also cleans up private-keys
        /// </summary>
        public static void RemoveCertificateFromStore(string thumbprint, StoreLocation storeLocation, string storeName)
        {
            using (AcquireSemaphore())
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
                    var keyProvInfo =
                        certificateHandle.GetCertificateProperty<KeyProviderInfo>(CertificateProperty.KeyProviderInfo);

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
        }

        public static ICollection<string> GetStoreNames(StoreLocation location)
        {
            var callback = new CertEnumSystemStoreCallBackProto(CertEnumSystemStoreCallBack);
            var names = new List<string>();
            CertificateSystemStoreLocation locationFlags;

            switch (location)
            {
                case StoreLocation.CurrentUser:
                    locationFlags = CertificateSystemStoreLocation.CurrentUser;
                    break;
                case StoreLocation.LocalMachine:
                    locationFlags = CertificateSystemStoreLocation.LocalMachine;
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

        static SafeCertContextHandle ImportPfxToStore(CertificateSystemStoreLocation storeLocation, string storeName, byte[] pfxBytes, string password,
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

            // Import the first certificate into the specified store
            AddCertificateToStore(storeLocation, storeName, certificates.First());

            // Any other certificates in the chain are imported into the Intermediate Authority and Root stores
            // of the Local Machine (importing into user CA stores causes a security-warning dialog to be shown)
            for (var i = 1; i < certificates.Count; i++)
            {
                var certificate = certificates[i];

                // If it is the last certificate in the chain and is self-signed then it goes into the Root store
                if (i == certificates.Count - 1 && IsSelfSigned(certificate))
                {
                    AddCertificateToStore(CertificateSystemStoreLocation.LocalMachine, RootAuthorityStoreName, certificate);
                    continue;
                }

                // Otherwise into the Intermediate Authority store
                AddCertificateToStore(CertificateSystemStoreLocation.LocalMachine, IntermediateAuthorityStoreName, certificate);
            }

            return certificates.First();
        }

        private static readonly IList<string> EnumeratedStoreNames = new List<string>();
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

        static void AddCertificateToStore(CertificateSystemStoreLocation storeLocation, string storeName, SafeCertContextHandle certificate)
        {
            try
            {
                using (var store = CertOpenStore(CertStoreProviders.CERT_STORE_PROV_SYSTEM, IntPtr.Zero, IntPtr.Zero,
                    storeLocation, storeName))
                {
                    var subjectName = CertificatePal.GetSubjectName(certificate);
                    
                    var storeContext = IntPtr.Zero;
                    if (!CertAddCertificateContextToStore(store, certificate,
                        AddCertificateDisposition.CERT_STORE_ADD_NEW, ref storeContext))
                    {
                        var error = Marshal.GetLastWin32Error();

                        if (error == (int) CapiErrorCode.CRYPT_E_EXISTS)
                        {
                            Log.Info($"Certificate '{subjectName}' already exists in store '{storeName}'.");
                            return;
                        }

                        throw new CryptographicException(error);
                    }

                    Log.Info($"Imported certificate '{subjectName}' into store '{storeName}'");
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

                        var certificateHandle = CertFindCertificateInStore(memoryStore,
                            CertificateEncodingType.Pkcs7OrX509AsnEncoding,
                            IntPtr.Zero, CertificateFindType.Sha1Hash, ref thumbprintData, IntPtr.Zero);

                        if (certificateHandle == null || certificateHandle.IsInvalid)
                            throw new Exception("Could not find certificate");

                        certificates.Add(certificateHandle);

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

        static void AddPrivateKeyAccessRules(ICollection<PrivateKeyAccessRule> accessRules, SafeCertContextHandle certificate)
        {
            try
            {
                var keyProvInfo = certificate.GetCertificateProperty<KeyProviderInfo>(
                    CertificateProperty.KeyProviderInfo);

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

                foreach (var cryptoKeyAccessRule in accessRules.Select(r => r.ToCryptoKeyAccessRule()))
                {
                    security.AddAccessRule(cryptoKeyAccessRule);
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

        static void SetCspPrivateKeySecurity(SafeCertContextHandle certificate, ICollection<PrivateKeyAccessRule> accessRules)
        {
            using (var cspHandle = CertificatePal.GetCspPrivateKey(certificate))
            {
                var security = GetCspPrivateKeySecurity(certificate);

                foreach (var cryptoKeyAccessRule in accessRules.Select(r => r.ToCryptoKeyAccessRule()))
                {
                    security.AddAccessRule(cryptoKeyAccessRule);
                }

                var securityDescriptorBytes = security.GetSecurityDescriptorBinaryForm();

                if (!CryptSetProvParam(cspHandle, CspProperties.SecurityDescriptor,
                    securityDescriptorBytes, SecurityDesciptorParts.DACL_SECURITY_INFORMATION))
                {
                    throw new CryptographicException(Marshal.GetLastWin32Error());
                }
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

        static IList<Org.BouncyCastle.X509.X509Certificate> GetCertificatesToImport(byte[] pfxBytes, string password)
        {
            using (var memoryStream = new MemoryStream(pfxBytes))
            {
                var pkcs12Store = new Pkcs12Store(memoryStream, password?.ToCharArray() ?? "".ToCharArray());

                if (pkcs12Store.Count < 1)
                    throw new Exception("No certificates were found in PFX");

                var aliases = pkcs12Store.Aliases.Cast<string>().ToList();

                // Find the first bag which contains a private-key
                var keyAlias = aliases.FirstOrDefault(alias => pkcs12Store.IsKeyEntry(alias));

                if (keyAlias != null)
                {
                    return pkcs12Store.GetCertificateChain(keyAlias).Select(x => x.Certificate).ToList();

                }

                return new List<Org.BouncyCastle.X509.X509Certificate>
                {
                    pkcs12Store.GetCertificate(aliases.First()).Certificate
                };
            }
        }

        static bool IsSelfSigned(SafeCertContextHandle certificate)
        {
            var certificateInfo = (CERT_INFO)Marshal.PtrToStructure(certificate.CertificateContext.pCertInfo, typeof(CERT_INFO));
            return CertCompareCertificateName(CertificateEncodingType.Pkcs7OrX509AsnEncoding, ref certificateInfo.Subject, ref certificateInfo.Issuer);
        }

        static byte[] CalculateThumbprint(Org.BouncyCastle.X509.X509Certificate certificate)
        {
            var der = certificate.GetEncoded();
            return DigestUtilities.CalculateDigest("SHA1", der);
        }
    }
}
#endif