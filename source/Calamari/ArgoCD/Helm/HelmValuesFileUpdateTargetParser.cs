#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD.Helm
{
    public class HelmValuesFileUpdateTargetParser
    {
        readonly List<ApplicationSourceWithMetadata> helmSources;

        readonly ApplicationName appName;
        readonly string defaultRegistry;
        readonly Dictionary<string, string> annotations;
        readonly bool containsMultipleSources;

        public HelmValuesFileUpdateTargetParser(Application toUpdate, string defaultRegistry)
        {
            annotations = toUpdate.Metadata.Annotations;
            containsMultipleSources = toUpdate.Spec.Sources.Count > 1;
            helmSources = toUpdate.GetSourcesWithMetadata().Where(s => s.SourceType == SourceType.Helm).ToList();
            appName = toUpdate.Metadata.Name.ToApplicationName();
            this.defaultRegistry = defaultRegistry;
        }

        public (IReadOnlyCollection<HelmValuesFileImageUpdateTarget> Targets, IReadOnlyCollection<HelmSourceConfigurationProblem> Problems) GetExplicitValuesFilesToUpdate(ApplicationSourceWithMetadata helmSource)
        {
            var results = new [] {helmSource}
                              .Where(hs => hs.Source.Helm?.ValueFiles.Count > 0)
                              .Select(ExtractInlineValuesFilesForSource).ToArray();
            
            return (results.SelectMany(v => v.Targets).ToArray(), results.SelectMany(v => v.Problems).ToArray());
        }

        (IReadOnlyCollection<HelmValuesFileImageUpdateTarget> Targets, IReadOnlyCollection<HelmSourceConfigurationProblem> Problems) ExtractInlineValuesFilesForSource(ApplicationSourceWithMetadata source)
        {
            var definedPathsForSource = ScopingAnnotationReader.GetImageReplacePathsForApplicationSource(source.Source.Name.ToApplicationSourceName(), annotations, containsMultipleSources);
            
            var results = source.Source.Helm?.ValueFiles
                                .Where(file => !file.StartsWith('$'))
                                .Select(file => ProcessInlineValuesFile(source, file, definedPathsForSource)).ToArray()
                                ?? Array.Empty<(HelmValuesFileImageUpdateTarget? Target, HelmSourceConfigurationProblem? Problem)>();

            return (results.Where(t => t.Target != null).Select(v => v.Target!).ToArray(),
                    results.Where(t => t.Problem != null).Select(v => v.Problem!).Distinct().ToArray());
        }

        (HelmValuesFileImageUpdateTarget? Target, HelmSourceConfigurationProblem? Problem) ProcessInlineValuesFile(ApplicationSourceWithMetadata source, string file, IReadOnlyCollection<string> definedPathsForSource)
        {
            if (!definedPathsForSource.Any())
            {
                return (null, new HelmSourceIsMissingImagePathAnnotation(source.SourceIdentity));
            }

            return (new HelmValuesFileImageUpdateTarget(appName,
                                                        source.Source.Name?.ToApplicationSourceName(),
                                                        defaultRegistry,
                                                        source.Source.Path,
                                                        source.Source.RepoUrl,
                                                        source.Source.TargetRevision,
                                                        file,
                                                        definedPathsForSource), null);
        }

        public (IReadOnlyCollection<HelmValuesFileImageUpdateTarget> Targets, IReadOnlyCollection<HelmSourceConfigurationProblem> Problems) GetHelmTargetsForRefSource(ApplicationSourceWithMetadata refSource)
        {
            var targets = new List<HelmValuesFileImageUpdateTarget>();
            var problems = new HashSet<HelmSourceConfigurationProblem>();

            foreach (var helmSource in helmSources)
            {
                if (helmSource.Source.Helm == null)
                    continue;
                
                var definedPathsForSource = ScopingAnnotationReader.GetImageReplacePathsForApplicationSource(helmSource.Source.Name.ToApplicationSourceName(), annotations, containsMultipleSources);
                   
                foreach (var valueFile in helmSource.Source.Helm.ValueFiles)
                {
                    if (ReferencesRef(valueFile, refSource.Source.Ref!))
                    {
                        if (!definedPathsForSource.Any())
                        {
                            problems.Add(
                                         new HelmSourceIsMissingImagePathAnnotation(helmSource.SourceIdentity, refSource.SourceIdentity)
                                        );
                            continue;
                        }

                        var relativeFile = valueFile[(valueFile.IndexOf('/') + 1)..];

                        targets.Add(new HelmValuesFileImageUpdateTarget(appName,
                                                                        refSource.Source.Name.ToApplicationSourceName(),
                                                                        defaultRegistry,
                                                                        ArgoCDConstants.RefSourcePath,
                                                                        refSource.Source.RepoUrl,
                                                                        refSource.Source.TargetRevision,
                                                                        relativeFile,
                                                                        definedPathsForSource));
                    }
                }
            }

            return (targets, problems);
        }
        
        static bool ReferencesRef(string filePath, string refName)
        {
            return filePath.StartsWith($"${refName}/");
        }
    }
}
