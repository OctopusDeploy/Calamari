using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        where TAuthentication : ITargetDiscoveryAuthenticationScope
    {
        public TargetDiscoveryContext(TargetDiscoveryScope scope, TAuthentication authentication)
        {
            this.Scope = scope;
            this.Authentication = authentication;
        }

        public TargetDiscoveryScope Scope { get; set; }

        public TAuthentication Authentication { get; set; }
    }
}
