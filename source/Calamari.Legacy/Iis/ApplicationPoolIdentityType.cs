using System;

namespace Calamari.Legacy.Iis
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