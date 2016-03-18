namespace Calamari.Deployment.Retention
{
    public interface IRetentionPolicy
    {
        void ApplyRetentionPolicy(string retentionPolicySet, int? days, int? releases);
    }
}