using Calamari.Common.Plumbing.FileSystem;

namespace Calamari.Tests.ArgoCD.Git
{
    public static class RepositoryTestHelpers
    {
        public static void DeleteRepositoryDirectory(ICalamariFileSystem fileSystem, string path)
        {
            //Some files might be ReadOnly, clean up properly by removing the ReadOnly attribute
            foreach (var file in fileSystem.EnumerateFilesRecursively(path))
            {
                fileSystem.RemoveReadOnlyAttributeFromFile(file);
            }

            fileSystem.DeleteDirectory(path, FailureOptions.IgnoreFailure);
        }
    }
}