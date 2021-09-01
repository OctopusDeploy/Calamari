using System;
using Octopus.CoreUtilities;

namespace Sashimi.Server.Contracts.DeploymentTools
{
    public class InPathDeploymentTool : IDeploymentTool
    {
        public InPathDeploymentTool(string id, string? subFolder = null, string? toolPathVariableToSet = null, string[]? supportedPlatforms = null)
        {
            Id = id;
            SubFolder = subFolder == null ? Maybe<string>.None : Maybe<string>.Some(subFolder);
            ToolPathVariableToSet = toolPathVariableToSet == null ? Maybe<string>.None : Maybe<string>.Some(toolPathVariableToSet);
            SupportedPlatforms = supportedPlatforms ?? new string[0];
        }

        public string Id { get; }
        public Maybe<string> SubFolder { get; }
        public bool AddToPath => true;
        public Maybe<string> ToolPathVariableToSet { get; }
        public string[] SupportedPlatforms { get; }

        public virtual Maybe<DeploymentToolPackage> GetCompatiblePackage(string platform)
        {
            return new DeploymentToolPackage(this, Id).AsSome();
        }
    }
}