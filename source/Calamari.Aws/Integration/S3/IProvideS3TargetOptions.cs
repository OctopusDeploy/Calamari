using System.Collections.Generic;

namespace Calamari.Aws.Integration.S3
{
    public interface IProvideS3TargetOptions
    {
        IEnumerable<S3TargetPropertiesBase> GetOptions(S3TargetMode mode);
    }
}