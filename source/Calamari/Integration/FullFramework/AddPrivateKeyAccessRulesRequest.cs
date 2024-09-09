using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Calamari.Integration.Certificates;

namespace Calamari.Integration.FullFramework
{

    public interface IRequest
    {
    }

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
    }
}