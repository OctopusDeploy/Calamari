using System;
using Octopus.Core.Resources.Versioning;

namespace Calamari.Integration.Packages
{
    public enum FeedType
    {
        None = 0,
        NuGet,
        Docker,
        Maven
    }
}
