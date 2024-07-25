using System;

namespace Calamari.FullFrameworkTools.Iis
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