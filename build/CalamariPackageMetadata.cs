using Nuke.Common.ProjectModel;

namespace Calamari.Build;

public class CalamariPackageMetadata(Project project, string framework, string architecture)
{
    public Project Project { get; } = project;
    public string Framework { get; } = framework;
    public string Architecture { get; } = architecture;
}
