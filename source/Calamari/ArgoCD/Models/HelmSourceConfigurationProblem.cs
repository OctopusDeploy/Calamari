#if NET
using System;

namespace Calamari.ArgoCD.Models
{
    public abstract class HelmSourceConfigurationProblem
    {
    }

    public class HelmSourceIsMissingImagePathAnnotation : HelmSourceConfigurationProblem
    {
        public HelmSourceIsMissingImagePathAnnotation(ApplicationSourceName helmSourceName, Uri helmSourceRepoUrl)
            : this(helmSourceName, helmSourceRepoUrl, helmSourceName)
        {
        }

        public HelmSourceIsMissingImagePathAnnotation(ApplicationSourceName helmSourceName, Uri helmSourceRepoUrl, ApplicationSourceName scopingSourceName)
        {
            HelmSourceName = helmSourceName;
            HelmSourceRepoUrl = helmSourceRepoUrl;
            ScopingSourceName = scopingSourceName;
        }

        public ApplicationSourceName HelmSourceName { get; }
        public Uri HelmSourceRepoUrl { get; }

        public ApplicationSourceName ScopingSourceName { get; }

        bool Equals(HelmSourceIsMissingImagePathAnnotation other)
        {
            return Equals(HelmSourceName, other.HelmSourceName)
                   && Equals(HelmSourceRepoUrl, other.HelmSourceRepoUrl)
                   && Equals(ScopingSourceName, other.ScopingSourceName);
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
            return HashCode.Combine(HelmSourceName, HelmSourceRepoUrl, ScopingSourceName);
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
}
#endif