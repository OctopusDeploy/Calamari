using System.Collections.Generic;
using Octopus.CoreUtilities;

namespace Calamari.Testing.Tools;

public class BoostrapperModuleDeploymentTool : IDeploymentTool
{
    private readonly IReadOnlyList<string> modulePaths;

    public BoostrapperModuleDeploymentTool(
        string id,
        IReadOnlyList<string> modulePaths,
        params string[] supportedPlatforms)
    {
        this.modulePaths = modulePaths;
        this.Id = id;
        this.SupportedPlatforms = supportedPlatforms ?? new string[0];
    }

    public string Id { get; }

    public Maybe<string> SubFolder => Maybe<string>.None;

    public bool AddToPath => false;

    public Maybe<string> ToolPathVariableToSet => Maybe<string>.None;

    public string[] SupportedPlatforms { get; }

    public Maybe<DeploymentToolPackage> GetCompatiblePackage(
        string platform)
    {
        return platform != "win-x64" && platform != "netfx" ? Maybe<DeploymentToolPackage>.None : new DeploymentToolPackage((IDeploymentTool) this, this.Id, this.modulePaths).AsSome<DeploymentToolPackage>();
    }
}