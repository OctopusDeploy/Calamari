using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace Calamari.FullFrameworkTools.Contracts.WindowsCertStore
{
    public interface IWindowsX509CertificateStore
    {
        string? FindCertificateStore(string thumbprint, StoreLocation storeLocation);

        void ImportCertificateToStore(byte[] pfxBytes,
                                      string password,
                                      StoreLocation storeLocation,
                                      string storeName,
                                      bool privateKeyExportable);

        void AddPrivateKeyAccessRules(string thumbprint,
                                      StoreLocation storeLocation,
                                      ICollection<PrivateKeyAccessRule> privateKeyAccessRules);

        void AddPrivateKeyAccessRules(string thumbprint,
                                      StoreLocation storeLocation,
                                      string storeName,
                                      ICollection<PrivateKeyAccessRule> privateKeyAccessRules);

        void ImportCertificateToStore(byte[] pfxBytes,
                                      string password,
                                      string userName,
                                      string storeName,
                                      bool privateKeyExportable);
    }
}
