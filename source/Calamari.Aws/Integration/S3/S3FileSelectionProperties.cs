using Calamari.Aws.Deployment.Conventions;

namespace Calamari.Aws.Integration.S3
{
    public class S3FileSelectionProperties : S3TargetPropertiesBase
    {
        public S3FileSelectionTypes Type { get; set; }
    }
}