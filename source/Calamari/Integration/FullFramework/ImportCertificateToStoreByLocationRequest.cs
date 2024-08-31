using System;
using System.Security.Cryptography.X509Certificates;

namespace Calamari.Integration.FullFramework {

public class ImportCertificateToStoreByLocationRequest : IRequest
{
    public ImportCertificateToStoreByLocationRequest(byte[] pfxBytes, string password,  StoreLocation storeLocation,string storeName, bool privateKeyExportable)
    {
        PfxBytes = pfxBytes;
        Password = password;
        StoreLocation = storeLocation;
        StoreName = storeName;
        PrivateKeyExportable = privateKeyExportable;
    }

    public byte[] PfxBytes { get; set; }
    public string Password { get; set; }
    public StoreLocation StoreLocation { get; set; }
    public string StoreName { get; set; }
    public bool PrivateKeyExportable { get; set; }

}
}