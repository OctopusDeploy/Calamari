using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Kubernetes.Conventions;
using Newtonsoft.Json;

namespace Calamari.Kubernetes
{
    public class HelmCommandBuilder
    {
        protected readonly StringBuilder CommandStringBuilder = new StringBuilder();
        protected StringBuilder HelmExecutable = new StringBuilder("helm");

        protected IEnumerable<string> AdditionalValuesFiles(RunningDeployment deployment, ICalamariFileSystem fileSystem)
        {
            var variables = deployment.Variables;
            var packageReferenceNames = variables.GetIndexes(Deployment.SpecialVariables.Packages.PackageCollection);
            foreach (var packageReferenceName in packageReferenceNames)
            {
                var sanitizedPackageReferenceName = fileSystem.RemoveInvalidFileNameChars(packageReferenceName);
                var paths = variables.GetPaths(SpecialVariables.Helm.Packages.ValuesFilePath(packageReferenceName));

                foreach (var providedPath in paths)
                {
                    var packageId = variables.Get(Deployment.SpecialVariables.Packages.PackageId(packageReferenceName));
                    var version = variables.Get(Deployment.SpecialVariables.Packages.PackageVersion(packageReferenceName));
                    var relativePath = Path.Combine(sanitizedPackageReferenceName, providedPath);
                    var files = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, relativePath).ToList();

                    if (!files.Any() && string.IsNullOrEmpty(packageReferenceName)) // Chart archives have chart name root directory 
                    {
                        Log.Verbose($"Unable to find values files at path `{providedPath}`. " +
                                    "Chart package contains root directory with chart name, so looking for values in there.");
                        var chartRelativePath = Path.Combine(fileSystem.RemoveInvalidFileNameChars(packageId), relativePath);
                        files = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, chartRelativePath).ToList();
                    }

                    if (!files.Any())
                        throw new CommandException($"Unable to find file `{providedPath}` for package {packageId} v{version}");

                    foreach (var file in files)
                    {
                        var relative = file.Substring(Path.Combine(deployment.CurrentDirectory, sanitizedPackageReferenceName).Length);
                        Log.Info($"Including values file `{relative}` from package {packageId} v{version}");
                        yield return Path.GetFullPath(file);
                    }
                }
            }
        }

        protected bool TryAddRawValuesYaml(RunningDeployment deployment, out string fileName)
        {
            fileName = null;
            var yaml = deployment.Variables.Get(SpecialVariables.Helm.YamlValues);
            // ReSharper disable once InvertIf
            if (!string.IsNullOrWhiteSpace(yaml))
            {
                fileName = Path.Combine(deployment.CurrentDirectory, "rawYamlValues.yaml");
                File.WriteAllText(fileName, yaml);
                return true;
            }

            return false;
        }

        protected bool TryGenerateVariablesFile(RunningDeployment deployment, out string fileName)
        {
            fileName = null;
            var variables = deployment.Variables.Get(SpecialVariables.Helm.KeyValues, "{}");
            var values = JsonConvert.DeserializeObject<Dictionary<string, object>>(variables);
            if (!values.Any())
                return false;

            fileName = Path.Combine(deployment.CurrentDirectory, "explicitVariableValues.yaml");
            File.WriteAllText(fileName, RawValuesToYamlConverter.Convert(values));
            return true;
        }
    }
}