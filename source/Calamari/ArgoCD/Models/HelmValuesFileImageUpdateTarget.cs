#if NET
using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Models
{
    public class HelmValuesFileImageUpdateTarget : ArgoCDImageUpdateTarget
    {
        public HelmValuesFileImageUpdateTarget(ApplicationName appName,
                                               ApplicationSourceName sourceName,
                                               string defaultClusterRegistry,
                                               string path,
                                               Uri repoUrl,
                                               string targetRevision,
                                               string fileName,
                                               IReadOnlyCollection<string> imagePathDefinitions) : base(appName,
                                                                                                        sourceName,
                                                                                                        defaultClusterRegistry,
                                                                                                        path,
                                                                                                        repoUrl,
                                                                                                        targetRevision)
        {
            FileName = fileName;
            ImagePathDefinitions = imagePathDefinitions;
        }

        public string FileName { get; }
        public IReadOnlyCollection<string> ImagePathDefinitions { get; }
    }

    public abstract class HelmSourceConfigurationProblem
    {
    }

    public class HelmSourceIsMissingImagePathAnnotation : HelmSourceConfigurationProblem
    {
        public HelmSourceIsMissingImagePathAnnotation(ApplicationSourceName name, Uri repoUrl, ApplicationSourceName? refName, Uri? refRepoUrl)
        {
            Name = name;
            RepoUrl = repoUrl;
            RefName = refName;
            RefRepoUrl = refRepoUrl;
        }

        public ApplicationSourceName Name { get;  }
        public Uri RepoUrl { get;  }
        
        public ApplicationSourceName? RefName { get; }
        public Uri? RefRepoUrl { get; }

        bool Equals(HelmSourceIsMissingImagePathAnnotation other)
        {
            return Equals(Name, other.Name)
                   && Equals(RepoUrl, other.RepoUrl)
                   && Equals(RefName, other.RefName)
                   && Equals(RefRepoUrl, other.RefRepoUrl);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((HelmSourceIsMissingImagePathAnnotation)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, RepoUrl, RefName, RefRepoUrl);
        }

        public static bool operator ==(HelmSourceIsMissingImagePathAnnotation left, HelmSourceIsMissingImagePathAnnotation right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(HelmSourceIsMissingImagePathAnnotation left, HelmSourceIsMissingImagePathAnnotation right)
        {
            return !Equals(left, right);
        }
    }
    
    public class RefSourceIsMissing : HelmSourceConfigurationProblem
    {
        public RefSourceIsMissing(string @ref, ApplicationSourceName helmSourceName, Uri helmSourceRepoUrl)
        {
            Ref = @ref;
            HelmSourceName = helmSourceName;
            HelmSourceRepoUrl = helmSourceRepoUrl;
        }

        public string Ref { get; }
        
        public ApplicationSourceName HelmSourceName { get;  }
        public Uri HelmSourceRepoUrl { get;  }

        bool Equals(RefSourceIsMissing other)
        {
            return Ref == other.Ref
                   && Equals(HelmSourceName, other.HelmSourceName)
                   && Equals(HelmSourceRepoUrl, other.HelmSourceRepoUrl);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((RefSourceIsMissing)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Ref, HelmSourceName, HelmSourceRepoUrl);
        }

        public static bool operator ==(RefSourceIsMissing left, RefSourceIsMissing right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(RefSourceIsMissing left, RefSourceIsMissing right)
        {
            return !Equals(left, right);
        }
    }
}
#endif