#if NET
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
        readonly List<ApplicationSource> helmSources;
        readonly List<ApplicationSource> refSources;

        readonly ApplicationName appName;
        readonly string defaultRegistry;
        readonly Dictionary<string, string> annotations;
        readonly bool containsMultipleSources;

        public HelmValuesFileUpdateTargetParser(Application toUpdate, string defaultRegistry)
        {
            annotations = toUpdate.Metadata.Annotations;
            containsMultipleSources = toUpdate.Spec.Sources.Count > 1;
            //Only deal with explicit Helm sources for now to preserve previous behaviour
            helmSources = toUpdate.GetSourcesWithMetadata().Where(s => s.SourceType == SourceType.Helm && s.Source.Helm != null).Select(s => s.Source).ToList();
            refSources = toUpdate.GetSourcesWithMetadata().Where(s => s.SourceType == SourceType.Directory && s.Source.Ref != null).Select(s => s.Source).ToList();
            appName = toUpdate.Metadata.Name.ToApplicationName();
            this.defaultRegistry = defaultRegistry;
        }

        public (IReadOnlyCollection<HelmValuesFileImageUpdateTarget> Targets, IReadOnlyCollection<HelmSourceConfigurationProblem> Problems) GetValuesFilesToUpdate()
        {
            var results = helmSources
                              .Where(hs => hs.Helm?.ValueFiles.Count > 0)
                              .Select(ExtractValuesFilesForSource).ToArray();
            
            return (results.SelectMany(v => v.Targets).ToArray(), results.SelectMany(v => v.Problems).ToArray());
        }

        (IReadOnlyCollection<HelmValuesFileImageUpdateTarget> Targets, IReadOnlyCollection<HelmSourceConfigurationProblem> Problems) ExtractValuesFilesForSource(ApplicationSource source)
        {
            var definedPathsForSource = ScopingAnnotationReader.GetImageReplacePathsForApplicationSource(source.Name.ToApplicationSourceName(), annotations, containsMultipleSources);
            if (source.Helm == null)
                return new ValueTuple<IReadOnlyCollection<HelmValuesFileImageUpdateTarget>, IReadOnlyCollection<HelmSourceConfigurationProblem>>();
            
            var results = source.Helm.ValueFiles.Select(file => file.StartsWith('$')
                                                                ? ProcessRefValuesFile(source, file, definedPathsForSource)
                                                                : ProcessInlineValuesFile(source, file, definedPathsForSource)).ToArray();

            return (results.Where(t => t.Target != null).Select(v => v.Target!).ToArray(),
                    results.Where(t => t.Problem != null).Select(v => v.Problem!).Distinct().ToArray());
        }

        (HelmValuesFileImageUpdateTarget? Target, HelmSourceConfigurationProblem? Problem) ProcessInlineValuesFile(ApplicationSource source, string file, IReadOnlyCollection<string> definedPathsForSource)
        {
            if (!definedPathsForSource.Any())
            {
                return (null, new HelmSourceIsMissingImagePathAnnotation(source.Name.ToApplicationSourceName(), source.RepoUrl));
            }

            return (new HelmValuesFileImageUpdateTarget(appName,
                                                        source.Name?.ToApplicationSourceName(),
                                                        defaultRegistry,
                                                        source.Path,
                                                        source.RepoUrl,
                                                        source.TargetRevision,
                                                        file,
                                                        definedPathsForSource), null);
        }

        (HelmValuesFileImageUpdateTarget? Target, HelmSourceConfigurationProblem? Problem) ProcessRefValuesFile(ApplicationSource source, string file, IReadOnlyCollection<string> definedPathsForSource)
        {
            var refName = GetRefFromFilePath(file);
            var refForValuesFile = refSources.FirstOrDefault(r => r.Ref == refName);
            if (refForValuesFile == null)
            {
                return (null, new RefSourceIsMissing(refName, source.Name.ToApplicationSourceName(), source.RepoUrl));
            }

            if (!definedPathsForSource.Any())
            {
                return (null,
                        new HelmSourceIsMissingImagePathAnnotation(source.Name.ToApplicationSourceName(), source.RepoUrl, refForValuesFile.Name.ToApplicationSourceName())
                    );
            }

            var relativeFile = file[(file.IndexOf('/') + 1)..];
            return (new HelmValuesFileImageUpdateTarget(appName,
                                                        refForValuesFile.Name.ToApplicationSourceName(),
                                                        defaultRegistry,
                                                        ArgoCDConstants.RefSourcePath,
                                                        refForValuesFile.RepoUrl,
                                                        refForValuesFile.TargetRevision,
                                                        relativeFile,
                                                        definedPathsForSource), null);
        }

        static string GetRefFromFilePath(string filePath)
        {
            return filePath.TrimStart('$')[..(filePath.IndexOf('/') - 1)];
        }
    }
}
#endif