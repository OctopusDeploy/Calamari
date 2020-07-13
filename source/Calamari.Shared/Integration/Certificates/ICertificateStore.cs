using System.Security.Cryptography.X509Certificates;
using Calamari.Common.Plumbing.Variables;
using Octostache;

namespace Calamari.Integration.Certificates
{
    public interface ICertificateStore
    {
        X509Certificate2 GetOrAdd(string thumbprint, byte[] bytes);
        X509Certificate2 GetOrAdd(string thumbprint, byte[] bytes, StoreName storeName);
        X509Certificate2 GetOrAdd(IVariables variables, string certificateVariable, string storeName, string storeLocation = "CurrentUser");
        X509Certificate2 GetOrAdd(IVariables variables, string certificateVariable, StoreName storeName, StoreLocation storeLocation = StoreLocation.CurrentUser);
    }
}