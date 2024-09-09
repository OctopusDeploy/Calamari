#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Principal;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Calamari.FullFrameworkTools.WindowsCertStore;

public class PrivateKeyAccessRule
{
    [JsonConstructor]
    public PrivateKeyAccessRule(string identity, PrivateKeyAccess access)
    {
        Identity = identity;
        Access = access;
    }
    
    public PrivateKeyAccess Access { get; }

    public string Identity { get; }
    
    
    public IdentityReference GetIdentityReference()
    {
        return TryParse(Identity, out var temp) ? temp! : new NTAccount(Identity);
    }
        
        
    public static bool TryParse(string value, out SecurityIdentifier? result)
    {
        try
        {
            result = new SecurityIdentifier(value);
            return true;
        }
        catch (ArgumentException)
        {
            result = null;
            return false;
        }
    }

}
