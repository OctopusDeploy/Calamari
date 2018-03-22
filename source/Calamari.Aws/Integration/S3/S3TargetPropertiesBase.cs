using System.Collections.Generic;

namespace Calamari.Aws.Integration.S3
{

    public interface IHaveMetadata
    {
        List<KeyValuePair<string, string>> Metadata { get; }
    }

    public interface IHaveTags
    {
        List<KeyValuePair<string, string>> Tags { get; }
    }

    public abstract class S3TargetPropertiesBase: IHaveMetadata, IHaveTags
    {
        public List<KeyValuePair<string, string>> Tags { get; set; } = new List<KeyValuePair<string, string>>();
        public List<KeyValuePair<string, string>> Metadata { get; set;  } = new List<KeyValuePair<string, string>>();
        public string CannedAcl { get; set; }
        public string StorageClass { get; set; }
    }
}