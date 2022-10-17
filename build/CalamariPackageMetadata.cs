using Nuke.Common.ProjectModel;

namespace Calamari.Build;

public class CalamariPackageMetadata
{
    public Project? Project { get; init; }
    public string? Framework { get; init; }
    public string? Architecture { get; init; }
    public bool IsCrossPlatform { get; init; }
    
}