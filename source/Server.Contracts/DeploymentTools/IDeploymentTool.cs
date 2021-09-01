using System;
using Octopus.CoreUtilities;

namespace Sashimi.Server.Contracts.DeploymentTools
{
    public interface IDeploymentTool
    {
        string Id { get; }
        Maybe<string> SubFolder { get; }
        bool AddToPath { get; }
        Maybe<string> ToolPathVariableToSet { get; }
        string[] SupportedPlatforms { get; }
        Maybe<DeploymentToolPackage> GetCompatiblePackage(string platform);
    }
}