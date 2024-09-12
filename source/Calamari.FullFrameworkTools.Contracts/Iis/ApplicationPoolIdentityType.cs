using System;

namespace Calamari.FullFrameworkTools.Contracts.Iis
{
    public enum ApplicationPoolIdentityType
    {
        ApplicationPoolIdentity,
        LocalService,
        LocalSystem,
        NetworkService,
        SpecificUser
    }
}