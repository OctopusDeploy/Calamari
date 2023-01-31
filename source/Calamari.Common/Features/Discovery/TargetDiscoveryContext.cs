using System;
using System.Collections.Generic;

namespace Calamari.Common.Features.Discovery
{
    public interface ITargetDiscoveryContext
    {
        public TargetDiscoveryScope Scope { get; }
    }

    /// <summary>
    /// This type and the types it uses are duplicated here from Octopus.Core, because:
    /// a) There is currently no existing project to place code shared between server and Calamari, and
    /// b) We expect a bunch of stuff in the Sashimi/Calamari space to be refactored back into the OctopusDeploy solution soon.
    /// </summary>
    public class TargetDiscoveryContext<TAuthentication> : ITargetDiscoveryContext
        where TAuthentication : class,ITargetDiscoveryAuthenticationDetails
    {
        public TargetDiscoveryContext(TargetDiscoveryScope? scope, TAuthentication? authentication, IReadOnlyDictionary<string, string>? additionalVariables)
        {
            Scope = scope;
            Authentication = authentication;
            AdditionalVariables = additionalVariables;
        }

        public TargetDiscoveryScope? Scope { get; set; }

        public TAuthentication? Authentication { get; set; }

        public IReadOnlyDictionary<string,string>? AdditionalVariables { get; set; }
    }
}
