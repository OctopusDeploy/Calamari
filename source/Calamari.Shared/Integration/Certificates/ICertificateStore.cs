using System.Security.Cryptography.X509Certificates;
using Octostache;

namespace Calamari.Integration.Certificates
{
    public interface ICertificateStore
    {
        X509Certificate2 GetOrAdd(string thumbprint, string bytes);
        X509Certificate2 GetOrAdd(string thumbprint, string bytes, StoreName storeName);
        X509Certificate2 GetOrAdd(VariableDictionary variables, string certificateVariable, string storeName, string storeLocation = "CurrentUser");
        X509Certificate2 GetOrAdd(VariableDictionary variables, string certificateVariable, StoreName storeName, StoreLocation storeLocation = StoreLocation.CurrentUser);
    }
}