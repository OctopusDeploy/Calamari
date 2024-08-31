#nullable enable
using System;
using System.Security.Cryptography.X509Certificates;
using Calamari.FullFrameworkTools.Command;

namespace Calamari.FullFrameworkTools.WindowsCertStore.Contracts;

public class FindCertificateStoreRequest : IRequest
{
    public FindCertificateStoreRequest(string thumbprint, StoreLocation storeLocation)
    {
        Thumbprint = thumbprint;
        StoreLocation = storeLocation;
    }

    public string Thumbprint { get; }
    public StoreLocation StoreLocation { get; }
    
    public VoidResponse DoIt(IWindowsX509CertificateStore certificateStore)
    {
        certificateStore.FindCertificateStore(Thumbprint, StoreLocation);
        return new VoidResponse();
    }
}