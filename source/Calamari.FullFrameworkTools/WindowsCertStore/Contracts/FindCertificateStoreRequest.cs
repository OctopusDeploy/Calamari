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
    
    public StringResponse DoIt(IWindowsX509CertificateStore certificateStore)
    {
        var result =certificateStore.FindCertificateStore(Thumbprint, StoreLocation);
        return new StringResponse() {
            Value = result
        };
    }
}