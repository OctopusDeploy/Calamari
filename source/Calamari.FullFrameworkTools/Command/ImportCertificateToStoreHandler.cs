using System;
using System.Security.Cryptography.X509Certificates;

namespace Calamari.FullFrameworkTools.Command
{
    public class ImportCertificateToStoreHandler : FullFrameworkToolCommandHandler<ImportCertificateToStoreRequest, ImportCertificateToStoreResponse>
    {
        public string Name => "import-certificate-to-store";

        protected override ImportCertificateToStoreResponse Handle(ImportCertificateToStoreRequest request)
        {
            throw new System.NotImplementedException();
        }
    }
    
    public class ImportCertificateToStoreRequest : IFullFrameworkToolRequest
    {
        public byte[] pfxBytes { get; set; }
        public string password { get; set; }
        StoreLocation storeLocation { get; set; }
        string storeName { get; set; }
        string PrivateKeyExportable { get; set; }
    }


    public class ImportCertificateToStoreResponse : IFullFrameworkToolResponse
    {
    }
}