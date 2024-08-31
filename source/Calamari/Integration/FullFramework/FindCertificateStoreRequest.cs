using System;
using System.Security.Cryptography.X509Certificates;

namespace Calamari.Integration.FullFramework
{

    public class FindCertificateStoreRequest : IRequest
    {
        public FindCertificateStoreRequest(string thumbprint, StoreLocation storeLocation)
        {
            Thumbprint = thumbprint;
            StoreLocation = storeLocation;
        }

        public string Thumbprint { get; }
        public StoreLocation StoreLocation { get; }
    }
}