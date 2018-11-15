using System.Security.Cryptography.X509Certificates;
using Octostache;

namespace Calamari.Integration.Certificates
{
    public interface ICertificateStore
    {
        X509Certificate2 GetOrAdd(string thumbprint, byte[] bytes, bool requirePrivateKey);
        X509Certificate2 GetOrAdd(string thumbprint, byte[] bytes, StoreName storeName, bool requirePrivateKey);
        X509Certificate2 GetOrAdd(VariableDictionary variables, string certificateVariable, bool requirePrivateKey, string storeName, string storeLocation = "CurrentUser");
        X509Certificate2 GetOrAdd(VariableDictionary variables, string certificateVariable, bool requirePrivateKey, StoreName storeName, StoreLocation storeLocation = StoreLocation.CurrentUser);
    }
}