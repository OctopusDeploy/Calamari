namespace Calamari.Azure.Deployment
{
    public static class AzureSpecialVariables
    {
        public static class BlobStorage
        {
            public const string FileSelections = "Octopus.Action.Azure.BlobStorage.FileSelections";
            public const string ContainerName = "Octopus.Action.Azure.BlobStorage.ContainerName";
            public const string Mode = "Octopus.Action.Azure.BlobStorage.Mode";
            public const string ResourceGroupName = "Octopus.Action.Azure.BlobStorage.ResourceGroupName";
        }
    }
}
