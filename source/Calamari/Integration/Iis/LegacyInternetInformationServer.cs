#if IIS_SUPPORT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Calamari.Integration.Certificates;
using Calamari.Integration.FullFramework;

namespace Calamari.Integration.Iis
{
    
    public class StringResponse  { public string Value { get; set; } }
    public class VoidResponse { }
    
    public class BoolResponse  { public bool Value { get; set; } }
    
    public class LegacyInternetInformationServer : IInternetInformationServer
    {
        readonly ILegacyFrameworkInvoker processInvoker;

        public LegacyInternetInformationServer(ILegacyFrameworkInvoker processInvoker)
        {
            this.processInvoker = processInvoker;
        }

        public bool OverwriteHomeDirectory(string iisWebSiteName, string path, bool legacySupport)
        {
            var cmd = new OverwriteHomeDirectoryRequest(iisWebSiteName, path, legacySupport);
            var response = processInvoker.Invoke<OverwriteHomeDirectoryRequest, BoolResponse>(cmd);
            return response.Value;
        }
    }
    
    public class LegacyWindowsX509CertificateStore : IWindowsX509CertificateStore
        {
            readonly ILegacyFrameworkInvoker processInvoker;

            public LegacyWindowsX509CertificateStore(ILegacyFrameworkInvoker processInvoker)
            {
                this.processInvoker = processInvoker;
            }
            
            public string FindCertificateStore(string thumbprint, StoreLocation storeLocation)
            {
                var cmd = new FindCertificateStoreRequest(thumbprint, storeLocation);
                var response = processInvoker.Invoke<FindCertificateStoreRequest, StringResponse>(cmd);
                return response.Value;
            }

            public void ImportCertificateToStore(byte[] pfxBytes,
                                                 string password,
                                                 StoreLocation storeLocation,
                                                 string storeName,
                                                 bool privateKeyExportable)
            {
                var cmd = new ImportCertificateToStoreByLocationRequest(pfxBytes, password, storeLocation, storeName, privateKeyExportable);
                processInvoker.Invoke<ImportCertificateToStoreByLocationRequest, VoidResponse>(cmd);
            
            }

            public void AddPrivateKeyAccessRules(string thumbprint, StoreLocation storeLocation, ICollection<PrivateKeyAccessRule> privateKeyAccessRules)
            {
                var cmd = new AddPrivateKeyAccessRulesRequest(thumbprint, storeLocation, null, privateKeyAccessRules.ToList());
                processInvoker.Invoke<AddPrivateKeyAccessRulesRequest, VoidResponse>(cmd);
            }

            public void AddPrivateKeyAccessRules(string thumbprint, StoreLocation storeLocation, string storeName, ICollection<PrivateKeyAccessRule> privateKeyAccessRules)
            {
                var cmd = new AddPrivateKeyAccessRulesRequest(thumbprint, storeLocation, storeName, privateKeyAccessRules.ToList());
                processInvoker.Invoke<AddPrivateKeyAccessRulesRequest, VoidResponse>(cmd);
            }

            public void ImportCertificateToStore(byte[] pfxBytes,
                                                 string password,
                                                 string userName,
                                                 string storeName,
                                                 bool privateKeyExportable)
            {
                   
                var cmd = new ImportCertificateToStoreByUserRequest(pfxBytes, password, userName, storeName, privateKeyExportable);
                processInvoker.Invoke<ImportCertificateToStoreByUserRequest, VoidResponse>(cmd);
            }
        }
}
#endif