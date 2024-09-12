using System;

namespace Calamari.FullFrameworkTools.Contracts.WindowsCertStore;

public class PrivateKeyAccessRule
{
    public PrivateKeyAccessRule(string identity, PrivateKeyAccess access)
    {
        Identity = identity;
        Access = access;
    }

    public string Identity { get; }
    public PrivateKeyAccess Access { get; }
}