using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace Calamari.Integration.Certificates
{
    /// <summary>
    /// Stand in replacement for IWindowsX509CertificateStore that will be registered for non Windows machines.
    /// This should never end up being called. If it is, something has gone wrong somewhere else
    /// </summary>
    public class NoOpWindowsX509CertificateStore: IWindowsX509CertificateStore
    {
        public string? FindCertificateStore(string thumbprint, StoreLocation storeLocation)
        {
            throw new System.NotImplementedException();
        }

        public void ImportCertificateToStore(byte[] pfxBytes,
                                             string password,
                                             StoreLocation storeLocation,
                                             string storeName,
                                             bool privateKeyExportable)
        {
            throw new System.NotImplementedException();
        }

        public void AddPrivateKeyAccessRules(string thumbprint, StoreLocation storeLocation, ICollection<PrivateKeyAccessRule> privateKeyAccessRules)
        {
            throw new System.NotImplementedException();
        }

        public void AddPrivateKeyAccessRules(string thumbprint, StoreLocation storeLocation, string storeName, ICollection<PrivateKeyAccessRule> privateKeyAccessRules)
        {
            throw new System.NotImplementedException();
        }

        public void ImportCertificateToStore(byte[] pfxBytes,
                                             string password,
                                             string userName,
                                             string storeName,
                                             bool privateKeyExportable)
        {
            throw new System.NotImplementedException();
        }
    }
}
