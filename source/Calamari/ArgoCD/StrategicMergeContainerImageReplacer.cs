using System.Collections.Generic;
using System.Text.Json;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD;

public class StrategicMergeContainerImageReplacer : IContainerImageReplacer
{
    class GroupValueKind
    {
        public string Group { get; set; }
        public string Version { get; set; }
        public string Kind { get; set; }
    }
    
    readonly string yamlContent;
    readonly string defaultRegistry;
    
    // Construct this statically
    readonly Dictionary<GroupValueKind, List<string>> resourceToImagePathList = new Dictionary<GroupValueKind, List<string>>() = {
        { new GroupValueKind("pod", "pod", "pod"), ["spec.Template.containers", "spec.Template.InitContainers"] } 
    }

    public StrategicMergeContainerImageReplacer(string yamlContent, string defaultRegistry)
    {
        this.yamlContent = yamlContent;
        this.defaultRegistry = defaultRegistry;
    }

    public ImageReplacementResult UpdateImages(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        var gvk = JsonSerializer.Deserialize<GroupValueKind>(yamlContent);

        List<string> pathsToUpdate = new List<string>();

        if (resourceToImagePathList.TryGetValue(gvk, out pathsToUpdate))
        {
            foreach (var path in pathsToUpdate)
            {
                //if it exists
                //update based on imagesToUpdate (see ContainerImageReplacer for how to do this)
            }
        }
        
        
        throw new System.NotImplementedException();
    }
}