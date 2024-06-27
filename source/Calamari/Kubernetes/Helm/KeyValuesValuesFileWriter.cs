using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;

namespace Calamari.Kubernetes.Helm
{
    public static class KeyValuesValuesFileWriter
    {
        const string KeyValuesFileNamePrefix = "explicitVariableValues";
        
        public static string WriteToFile(RunningDeployment deployment, ICalamariFileSystem fileSystem, Dictionary<string, object> keyValues, int? index = null)
        {
            if (!keyValues.Any())
            {
                return null;
            }

            var targetFilename = GetFileName(index);

            var fileName = Path.Combine(deployment.CurrentDirectory, targetFilename);
            fileSystem.WriteAllText(fileName, RawValuesToYamlConverter.Convert(keyValues));

            return fileName;
        }
        
        internal static string GetFileName(int? index) => HelmValuesFileUtils.GetUniqueFileName(KeyValuesFileNamePrefix, index);
    }
}