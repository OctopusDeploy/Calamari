using System.Collections.Generic;
using System.IO;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public record HelmValuesFileTarget(string RelativePath, IReadOnlyCollection<string>? AnnotationTemplates = null)
{
    public static HelmValuesFileTarget FromAnnotationTarget(HelmValuesFileImageUpdateTarget target)
        => new(Path.Combine(target.Path, target.FileName), target.ImagePathDefinitions);
}
