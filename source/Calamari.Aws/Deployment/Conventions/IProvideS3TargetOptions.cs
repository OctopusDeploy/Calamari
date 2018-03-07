using System.Collections.Generic;
using Calamari.Aws.Integration.S3;

namespace Calamari.Aws.Deployment.Conventions
{
    public interface IProvideS3TargetOptions
    {
        IEnumerable<S3TargetPropertiesBase> GetOptions(S3TargetMode mode);
    }
}