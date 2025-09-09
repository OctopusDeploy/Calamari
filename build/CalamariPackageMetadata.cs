using Nuke.Common.ProjectModel;

namespace Calamari.Build;

public class CalamariPackageMetadata
{
    public CalamariPackageMetadata(Project project, string framework, string architecture)
    {
        Project = project;
        Framework = framework;
        Architecture = architecture;
        IsCrossPlatform = isCrossPlatform;
    }

    public Project Project { get; }
    public string Framework { get; }
    public string Architecture { get; }    
}
