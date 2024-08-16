using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;

namespace Calamari.Kubernetes.Helm
{
    public static class InlineYamlValuesFileWriter
    {
        const string InlineYamlFileNamePrefix = "rawYamlValues";
        
        public static string WriteToFile(RunningDeployment deployment, ICalamariFileSystem fileSystem, string yaml, int? index = null)
        {
            if (string.IsNullOrWhiteSpace(yaml))
                return null;

            var targetFilename = GetFileName(index);

            var fileName = Path.Combine(deployment.CurrentDirectory, targetFilename);
            fileSystem.WriteAllText(fileName, yaml);

            return fileName;
        }
        
        internal static string GetFileName(int? index) => HelmValuesFileUtils.GetUniqueFileName(InlineYamlFileNamePrefix, index);
    }
}