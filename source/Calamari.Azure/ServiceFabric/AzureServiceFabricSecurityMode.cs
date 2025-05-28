using System;

namespace Calamari.Azure.ServiceFabric
{
    enum AzureServiceFabricSecurityMode
    {
        Unsecure,
        SecureClientCertificate,
        SecureAzureAD,
        SecureAD,
    }
}