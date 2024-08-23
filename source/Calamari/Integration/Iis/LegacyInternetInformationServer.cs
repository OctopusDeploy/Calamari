#if IIS_SUPPORT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Calamari.Common.Plumbing.Logging;
using Calamari.FullFrameworkTools.WindowsCertStore;
using Calamari.FullFrameworkTools.WindowsCertStore.WindowsNative;
using ILog = Calamari.FullFrameworkTools.Command.ILog;

namespace Calamari.Integration.Iis
{
    class InnerLog : ILog
    {
        public void Verbose(string message)
        {
            Log.Verbose(message);
        }

        public void Error(string message)
        {
            Log.Error(message);
        }

        public void Info(string message)
        {
            Log.Info(message);
        }

        public void Fatal(Exception exception)
        {
            throw new NotImplementedException("This should not be handled in-process");
        }

        public void Result(object response)
        {
            throw new NotImplementedException("This should not be handled in-process");
        }
    }

    public class LegacyInternetInformationServer : IInternetInformationServer
    {
        readonly InProcessInvoker processInvoker;

        public LegacyInternetInformationServer(InProcessInvoker processInvoker)
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
            readonly InProcessInvoker processInvoker;

            public LegacyWindowsX509CertificateStore(InProcessInvoker processInvoker)
            {
                this.processInvoker = processInvoker;
            }
            
            public string FindCertificateStore(string thumbprint, StoreLocation storeLocation)
            {
                var cmd = new FindCertificateStoreRequest(thumbprint, storeLocation);
                var response = processInvoker.Invoke<FindCertificateStoreRequest, StringResponse>(cmd);
                return response.Valus;
            }

            public void ImportCertificateToStore(byte[] pfxBytes,
                                                 string password,
                                                 StoreLocation storeLocation,
                                                 string storeName,
                                                 bool privateKeyExportable)
            {
                
            
            }

            public void AddPrivateKeyAccessRules(string thumbprint, StoreLocation storeLocation, ICollection<PrivateKeyAccessRule> privateKeyAccessRules)
            {
                throw new NotImplementedException();
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