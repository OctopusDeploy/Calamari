using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Helm;

public class UpdateUsingStepVariables
{
    readonly ILog log;
    readonly ICalamariFileSystem fileSystem;

    public UpdateUsingStepVariables(ILog log, ICalamariFileSystem fileSystem)
    {
        this.log = log;
        this.fileSystem = fileSystem;
    }

    SourceUpdateResult UpdateImageTagsInValuesFiles(IReadOnlyCollection<string> valuesFilesToUpdate, IReadOnlyCollection<PackageAndHelmReference> stepReferencedContainers, string defaultRegistry, UpdateArgoCDAppDeploymentConfig deploymentConfig, RepositoryWrapper repository, ApplicationSourceWithMetadata sourceWithMetadata)
        {
            var filesUpdated = new HashSet<string>();
            var imagesUpdated = new HashSet<string>();
            foreach (var valuesFile in valuesFilesToUpdate)
            {
                var wasUpdated = false;
                var repoRelativePath = Path.Combine(repository.WorkingDirectory, valuesFile);
                log.Info($"Processing file at {valuesFile}.");
                var fileContent = fileSystem.ReadFile(repoRelativePath);
                var originalYamlParser = new HelmYamlParser(fileContent); // Parse and track the original yaml so that content can be read from it.
                var flattenedYamlPathDictionary = HelmValuesEditor.CreateFlattenedDictionary(originalYamlParser);
                foreach (var container in stepReferencedContainers.Where(c => c.HelmReference is not null))
                {
                    if (flattenedYamlPathDictionary.TryGetValue(container.HelmReference!, out var valueToUpdate))
                    {
                        if (IsUnstructuredText(valueToUpdate))
                        {
                            HelmValuesEditor.UpdateNodeValue(fileContent, container.HelmReference!, container.ContainerReference.Tag);
                            filesUpdated.Add(valuesFile);
                            imagesUpdated.Add(container.ContainerReference.ToString());
                        }
                        else
                        {
                            var cir = ContainerImageReference.FromReferenceString(valueToUpdate, defaultRegistry);
                            var comparison = container.ContainerReference.CompareWith(cir);
                            if (comparison.MatchesImage())
                            {
                                if (!comparison.TagMatch)
                                {
                                    var newValue = cir.WithTag(container.ContainerReference.Tag);
                                    fileContent = HelmValuesEditor.UpdateNodeValue(fileContent, container.HelmReference!, newValue);
                                    wasUpdated = true;
                                    filesUpdated.Add(valuesFile);
                                    imagesUpdated.Add(newValue);
                                }
                            }
                            else
                            {
                                log.Warn($"Attempted to update value entry '{container.HelmReference}', however it contains a mismatched image name and registry.");
                            }
                        }
                        if (wasUpdated)
                        {
                            fileSystem.WriteAllText(repoRelativePath, fileContent);
                        }
                    }
                }
            }

            return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
        }
    
    
    static bool IsUnstructuredText(string content)
    {
        var lastColonIndex = content.LastIndexOf(':');
        var lastSlashIndex = content.LastIndexOf('/');

        return lastColonIndex == -1 && lastSlashIndex == -1;
    }
}