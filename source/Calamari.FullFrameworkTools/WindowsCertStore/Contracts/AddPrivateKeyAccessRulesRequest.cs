#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Calamari.FullFrameworkTools.Command;

namespace Calamari.FullFrameworkTools.WindowsCertStore.Contracts;

public class AddPrivateKeyAccessRulesRequest : IRequest
{
    public AddPrivateKeyAccessRulesRequest(string thumbprint, StoreLocation storeLocation, string storeName, List<PrivateKeyAccessRule> privateKeyAccessRules)
    {
        this.Thumbprint = thumbprint;
        StoreName = storeName;
        PrivateKeyAccessRules = privateKeyAccessRules;
        StoreLocation = storeLocation;
    }

    public string Thumbprint { get; set; }
    public StoreLocation StoreLocation { get; set; }
    public string StoreName { get; set; }
    public List<PrivateKeyAccessRule> PrivateKeyAccessRules { get; set; }
    
    
    public VoidResponse DoIt(IWindowsX509CertificateStore certificateStore)
    {
        if (string.IsNullOrEmpty(StoreName))
        {
            certificateStore.AddPrivateKeyAccessRules(Thumbprint, StoreLocation, PrivateKeyAccessRules);    
        }
        else
        {
            certificateStore.AddPrivateKeyAccessRules(Thumbprint, StoreLocation, StoreName, PrivateKeyAccessRules);
        }

        return new VoidResponse();
    }
}