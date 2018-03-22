namespace Calamari.Aws.Integration.S3
{
    public class S3SingleFileSlectionProperties : S3FileSelectionProperties
    {
        public string BucketKey { get; set; }
        public string Path { get; set; }
        public bool PerformVariableSubstitution { get; set; }
    }
}