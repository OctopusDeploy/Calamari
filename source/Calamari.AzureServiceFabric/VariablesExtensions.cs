using System;
using System.Security.Cryptography.X509Certificates;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureServiceFabric;

public static class VariablesExtensions {

    public static StoreName GetServiceFabricCertificateStoreName(this IVariables variables)
    {
        return Enum.TryParse(variables.Get(SpecialVariables.Action.ServiceFabric.CertificateStoreName, nameof(StoreName.My)), out StoreName storeName) ?
            storeName :
            StoreName.My;
    }
        
    public static StoreLocation GetServiceFabricCertificateStoreLocation(this IVariables variables)
    {
        return Enum.TryParse(variables.Get(SpecialVariables.Action.ServiceFabric.CertificateStoreLocation, nameof(StoreLocation.LocalMachine)), out StoreLocation storeLocation) ?
            storeLocation :
            StoreLocation.LocalMachine;
    }
}