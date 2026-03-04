using System;

namespace Calamari.ArgoCD.Models
{
    public abstract class HelmSourceConfigurationProblem
    {
    }

    public class HelmSourceIsMissingImagePathAnnotation : HelmSourceConfigurationProblem
    {
        public HelmSourceIsMissingImagePathAnnotation(string sourceIdentity, string refSourceIdentity = null)
        {
            SourceIdentity = sourceIdentity;
            RefSourceIdentity = refSourceIdentity;
        }

        public string SourceIdentity { get; }
        public string RefSourceIdentity { get; }

        bool Equals(HelmSourceIsMissingImagePathAnnotation other)
        {
            return Equals(SourceIdentity, other.SourceIdentity)
                   && Equals(RefSourceIdentity, other.RefSourceIdentity);
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
            return HashCode.Combine(SourceIdentity, RefSourceIdentity);
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
