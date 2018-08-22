namespace Calamari.Azure.Deployment
{
    public static class AzureSpecialVariables
    {
        public static class BlobStorage
        {
            public const string FileSelections = "Octopus.Action.Azure.BlobStorage.FileSelections";
            public const string GlobsSelection = "Octopus.Action.Azure.BlobStorage.GlobsSelection";
            public const string UploadPackage = "Octopus.Action.Azure.BlobStorage.UploadPackage";
            public const string SubstitutionPatterns = "Octopus.Action.Azure.BlobStorage.SubstitutionPatterns";
        }
    }
}
