#nullable enable
using System;
using Calamari.FullFrameworkTools.Command;

namespace Calamari.FullFrameworkTools.WindowsCertStore.Contracts;

public class ImportCertificateToStoreByUserRequest : IRequest
{
    public ImportCertificateToStoreByUserRequest(byte[] pfxBytes, string password, string userName, string storeName, bool privateKeyExportable)
    {
        PfxBytes = pfxBytes;
        Password = password;
        UserName = userName;
        StoreName = storeName;
        PrivateKeyExportable = privateKeyExportable;
    }

    public byte[] PfxBytes { get; set; }
    public string Password { get; set; }
    public string UserName { get; set; }
    public string StoreName { get; set; }
    public bool PrivateKeyExportable { get; set; }

    public VoidResponse DoIt(IWindowsX509CertificateStore certificateStore)
    {
        certificateStore.ImportCertificateToStore(PfxBytes, Password, UserName, StoreName, PrivateKeyExportable);
        return new VoidResponse();
    }
}