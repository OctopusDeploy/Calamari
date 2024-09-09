#nullable enable
using System;
using System.Security.Cryptography.X509Certificates;
using Calamari.FullFrameworkTools.Command;

namespace Calamari.FullFrameworkTools.WindowsCertStore.Contracts;

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

    public VoidResponse DoIt(IWindowsX509CertificateStore certificateStore)
    {
        certificateStore.ImportCertificateToStore(PfxBytes, Password, StoreLocation, StoreName, PrivateKeyExportable);
        return new VoidResponse();
    }
}