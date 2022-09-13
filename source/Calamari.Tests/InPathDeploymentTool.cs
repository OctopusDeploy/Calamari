using Calamari.Testing;
using Octopus.CoreUtilities;

namespace Calamari.AzureScripting.Tests;

public class InPathDeploymentTool : IDeploymentTool
{
    public InPathDeploymentTool(
        string id,
        string? subFolder = null,
        string? toolPathVariableToSet = null,
        string[]? supportedPlatforms = null)
    {
        this.Id = id;
        this.SubFolder = subFolder == null ? Maybe<string>.None : Maybe<string>.Some(subFolder);
        this.ToolPathVariableToSet = toolPathVariableToSet == null ? Maybe<string>.None : Maybe<string>.Some(toolPathVariableToSet);
        this.SupportedPlatforms = supportedPlatforms ?? new string[0];
    }

    public string Id { get; }

    public Maybe<string> SubFolder { get; }

    public bool AddToPath => true;

    public Maybe<string> ToolPathVariableToSet { get; }

    public string[] SupportedPlatforms { get; }

    public virtual Maybe<DeploymentToolPackage> GetCompatiblePackage(
        string platform)
    {
        return new DeploymentToolPackage((IDeploymentTool) this, this.Id).AsSome<DeploymentToolPackage>();
    }
}