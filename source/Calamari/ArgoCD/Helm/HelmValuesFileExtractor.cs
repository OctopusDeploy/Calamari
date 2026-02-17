#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD.Helm
{
    public class HelmValuesFileExtractor
    {
        readonly List<ApplicationSourceWithMetadata> helmSources;
        
        readonly string defaultRegistry;

        public HelmValuesFileExtractor(Application toUpdate, string defaultRegistry)
        {
            helmSources = toUpdate.GetSourcesWithMetadata().Where(s => s.SourceType == SourceType.Helm).ToList();
            this.defaultRegistry = defaultRegistry;
        }

        public IReadOnlyCollection<string> GetInlineValuesFilesReferencedByHelmSource(ApplicationSourceWithMetadata helmSource)
        {
            return helmSource.Source.Helm?.ValueFiles.Where(file => !file.StartsWith('$'))
                             .Select(vf => Path.Combine(helmSource.Source.Path!, vf))
                             .ToList()
                   ?? [];
        }

        public IReadOnlyCollection<string> GetValueFilesReferencedInRefSource(ApplicationSourceWithMetadata refSource)
        {
            var refPrefix = $"${refSource.Source.Ref!}/";
            return helmSources
                                        .Where(hs => hs.Source.Helm != null)
                                        .SelectMany(hs => hs.Source.Helm!.ValueFiles.Where(f => f.StartsWith(refPrefix)))
                                        .Distinct()
                                        .Select(vf => vf.Substring(refPrefix.Length))
                                        .ToList();
        }
    }
}
