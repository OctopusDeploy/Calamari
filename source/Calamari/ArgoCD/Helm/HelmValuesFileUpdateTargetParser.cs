#if NET
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;
using Calamari.ArgoCD.Domain;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Helm
{

    public class HelmValuesFileUpdateTargetParser
    {
        readonly List<KeyValuePair<string, string>> aliasAnnotations;

        readonly List<KeyValuePair<string, string>> imageReplacePathAnnotations;

        readonly List<HelmSource> helmSources;
        readonly List<ReferenceSource> refSources;

        readonly string appName;
        readonly string defaultRegistry;

        public HelmValuesFileUpdateTargetParser(Application toUpdate, string defaultRegistry)
        {
            aliasAnnotations = toUpdate.Metadata.Annotations
                                            .Where(a => a.Key.StartsWith(ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey))
                                            .ToList();
            imageReplacePathAnnotations = toUpdate.Metadata.Annotations
                                                       .Where(a => a.Key.StartsWith(ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey))
                                                       .ToList();
            helmSources = toUpdate.Spec.Sources.OfType<HelmSource>().ToList();
            refSources = toUpdate.Spec.Sources.OfType<ReferenceSource>().ToList();
            appName = toUpdate.Metadata.Name;
            this.defaultRegistry = defaultRegistry;
        }

        public List<HelmValuesFileImageUpdateTarget> GetValuesFilesToUpdate()
        {
            return helmSources
                   .Where(hs => hs.Helm.ValueFiles.Count > 0)
                   .SelectMany(ExtractValuesFilesForSource)
                   .ToList();
        }

        List<HelmValuesFileImageUpdateTarget> ExtractValuesFilesForSource(HelmSource source)
        {
            return source.Helm.ValueFiles.Select(file => file.StartsWith('$')
                                                ? ProcessRefValuesFile(file)
                                                : ProcessInlineValuesFile(source, file))
                         .OfType<HelmValuesFileImageUpdateTarget>()
                         .ToList();
        }

        HelmValuesFileImageUpdateTarget? ProcessInlineValuesFile(HelmSource source, string file)
        {
            // Check if there is an unaliased annotation
            var definedPathsForSource = imageReplacePathAnnotations
                                        .FirstOrDefault(a => a.Key == $"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}")
                                        .Value;
            if (definedPathsForSource != null)
            {
                if (source.Helm.ValueFiles.Count == 1)
                {
                    return new HelmValuesFileImageUpdateTarget(appName,
                                                               defaultRegistry,
                                                               source.Path,
                                                               source.RepoUrl,
                                                               source.TargetRevision,
                                                               file,
                                                               ConvertAnnotationToList(definedPathsForSource));
                }

                var valueFilesPathsString = string.Join(", ", source.GenerateValuesFilePaths());
                throw new InvalidHelmImageReplaceAnnotationsException($"Cannot use {ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey} without an alias when multiple inline values files are present.\n Values Files: {valueFilesPathsString}");
            }

            // Check for aliased annotation for file
            var aliasKeyForFile = aliasAnnotations.FirstOrDefault(a => a.Value == file).Key;
            string? alias = null;
            if (aliasKeyForFile != null)
            {
                alias = GetSpecifierFromKey(aliasKeyForFile);
            }
            else
            {
                // Check if there is an alias with an absolute path value
                var absoluteAliasPath = source.GenerateInlineValuesAbsolutePath(file);
                var aliasKeyForSource = aliasAnnotations.FirstOrDefault(a => a.Value == absoluteAliasPath).Key;
                if (aliasKeyForSource != null)
                {
                    alias = GetSpecifierFromKey(aliasKeyForSource);
                }
            }

            if (alias != null)
            {
                var definedPathsForAlias = GetKeyedReplacementPathAnnotation(alias);
                if (definedPathsForAlias != null)
                {
                    return new HelmValuesFileImageUpdateTarget(appName,
                                                               defaultRegistry,
                                                               source.Path,
                                                               source.RepoUrl,
                                                               source.TargetRevision,
                                                               file,
                                                               ConvertAnnotationToList(definedPathsForAlias));
                }

                // Invalid state - alias defined but without corresponding Path annotation 
                return new InvalidHelmValuesFileImageUpdateTarget(appName,
                                                                  defaultRegistry,
                                                                  source.Path,
                                                                  source.RepoUrl,
                                                                  source.TargetRevision,
                                                                  file,
                                                                  alias);
            }

            return null;
        }

        HelmValuesFileImageUpdateTarget? ProcessRefValuesFile(string file)
        {
            var refName = GetRefFromFilePath(file);
            var refForValuesFile = refSources.FirstOrDefault(r => r.Ref == refName);
            if (refForValuesFile == null)
            {
                // Invalid Ref used in Helm Config (which we should get because it never would have deployed properly anyway
                throw new InvalidHelmImageReplaceAnnotationsException($"File: {file} references a Ref sources that could not be found.");
            }

            string? imageReplacementPathsForFile = null;
            // Check for an alias first as these take precedence
            var aliasWithRefValueKey = aliasAnnotations.FirstOrDefault(a => a.Value == file).Key;
            if (aliasWithRefValueKey != null)
            {
                // We found an alias, let's try to work with that
                var alias = GetSpecifierFromKey(aliasWithRefValueKey);
                imageReplacementPathsForFile = GetKeyedReplacementPathAnnotation(alias);

                if (string.IsNullOrEmpty(imageReplacementPathsForFile))
                {
                    // Invalid state - alias defined but without corresponding Path annotation 
                    return new InvalidHelmValuesFileImageUpdateTarget(appName,
                                                                      defaultRegistry,
                                                                      ArgoCDConstants.RefSourcePath,
                                                                      refForValuesFile.RepoUrl,
                                                                      refForValuesFile.TargetRevision,
                                                                      file,
                                                                      alias);
                }
            }

            // No values for Alias, let's see if there are annotations for the Ref directly
            imageReplacementPathsForFile ??= GetKeyedReplacementPathAnnotation(refName);
            if (!string.IsNullOrEmpty(imageReplacementPathsForFile))
            {
                var relativeFile = file[(file.IndexOf('/') + 1)..];
                return new HelmValuesFileImageUpdateTarget(appName,
                                                           defaultRegistry,
                                                           ArgoCDConstants.RefSourcePath,
                                                           refForValuesFile.RepoUrl,
                                                           refForValuesFile.TargetRevision,
                                                           relativeFile,
                                                           ConvertAnnotationToList(imageReplacementPathsForFile));
            }

            // No alias and no ref keyed replacement paths - ignore
            return null;
        }

        static List<string> ConvertAnnotationToList(string annotationValue)
        {
            return annotationValue.Split(',').Select(a => a.Trim()).ToList();
        }

        static string GetSpecifierFromKey(string key)
        {
            return key[(key.LastIndexOf('.') + 1)..];
        }

        static string GetRefFromFilePath(string filePath)
        {
            return filePath.TrimStart('$')[..(filePath.IndexOf('/') - 1)];
        }

        string? GetKeyedReplacementPathAnnotation(string key)
        {
            return imageReplacePathAnnotations
                   .FirstOrDefault(a => a.Key == ArgoCDConstants.Annotations.OctopusImageReplacementPathsKeyWithSpecifier(key))
                   .Value;
        }
    }
}
#endif
