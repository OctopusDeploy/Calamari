using System;
using System.Collections.Generic;
using Octopus.Server.Extensibility.HostServices.Model;

namespace Sashimi.Server.Contracts.ActionHandlers.Validation
{
    public class DeploymentActionValidationContext
    {
        public DeploymentActionValidationContext(string actionType,
                                                 IReadOnlyDictionary<string, string> properties,
                                                 IReadOnlyCollection<PackageReference> packages)
        {
            ActionType = actionType;
            Properties = properties;
            Packages = packages;
        }

        public string ActionType { get; }
        public IReadOnlyDictionary<string, string> Properties { get; }
        public IReadOnlyCollection<PackageReference> Packages { get; }
    }
}