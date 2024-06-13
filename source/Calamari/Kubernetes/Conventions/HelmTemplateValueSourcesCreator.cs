using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.Conventions
{
    public static class HelmTemplateValueSourcesCreator
    {
        public static IEnumerable<string> ParseTemplateValueSources(RunningDeployment deployment, ICalamariFileSystem fileSystem, ILog log)
        {
            var templateValueSources = deployment.Variables.Get(SpecialVariables.Helm.TemplateValuesSources);

            if (string.IsNullOrWhiteSpace(templateValueSources))
                return Enumerable.Empty<string>();

            var parsedJsonArray = JArray.Parse(templateValueSources);

            var filenames = new List<string>();
            foreach (var json in parsedJsonArray)
            {
                var tvs = json.ToObject<TemplateValuesSource>();
                switch (tvs.Type)
                {
                    case TemplateValuesSourceType.Chart:
                        var chartTvs = json.ToObject<ChartTemplateValuesSource>();
                        var chartFilenames =  GenerateAndWriteChartValues(deployment,fileSystem,log, chartTvs.ValuesFilePaths);
                        filenames.AddRange(chartFilenames);
                        
                        break;
                    case TemplateValuesSourceType.KeyValues:
                        var keyValuesTvs = json.ToObject<KeyValuesTemplateValuesSource>();
                        var keyValueFilename = GenerateAndWriteKeyValues(deployment, keyValuesTvs.Value);
                        AddIfNotNull(filenames, keyValueFilename);
                        
                        break;
                    
                    case TemplateValuesSourceType.Package:
                        var packageTvs = json.ToObject<PackageTemplateValuesSource>();
                        var packageFilenames = GenerateAndWritePackageValues(deployment,fileSystem,log, packageTvs.ValuesFilePaths,packageTvs.PackageId, packageTvs.PackageName);
                        filenames.AddRange(packageFilenames);
                        
                        break;
                    case TemplateValuesSourceType.InlineYaml:
                        var inlineYamlTvs = json.ToObject<InlineYamlTemplateValuesSource>();
                        var inlineYamlFilename = GenerateAndWriteInlineYaml(deployment, inlineYamlTvs.Value);
                        AddIfNotNull(filenames, inlineYamlFilename);
                        
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return filenames;
        }

        public static IEnumerable<string> GenerateAndWritePackageValues(RunningDeployment deployment,
                                                                 ICalamariFileSystem fileSystem,
                                                                 ILog log,
                                                                 string valuesFilePaths,
                                                                 string packageId,
                                                                 string packageName)
        {
            var variables = deployment.Variables;
            var packageNames = variables.GetIndexes(PackageVariables.PackageCollection);
            if (!packageNames.Contains(packageName))
            {
                return null;
            }
            
            var packageIdFromVariables = variables.Get(PackageVariables.IndexedPackageId(packageName));
            if (string.IsNullOrEmpty(packageIdFromVariables) || !packageIdFromVariables.Equals(packageId, StringComparison.CurrentCultureIgnoreCase))
            {
                return null;
            }
            
            var filenames = new List<string>();
            var errors = new List<string>();

            var sanitizedPackageReferenceName = PackageName.ExtractPackageNameFromPathedPackageId(fileSystem.RemoveInvalidFileNameChars(packageName));
            var valuesPaths = variables.GetPaths(valuesFilePaths);
                
            foreach (var valuePath in valuesPaths)
            {
                var pathedPackedName = PackageName.ExtractPackageNameFromPathedPackageId(packageId);
                var version = variables.Get(PackageVariables.IndexedPackageVersion(packageName));
                var relativePath = Path.Combine(sanitizedPackageReferenceName, valuePath);
                var currentFiles = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, relativePath).ToList();

                if (!currentFiles.Any() && string.IsNullOrEmpty(packageName)) // Chart archives have chart name root directory
                {
                    log.Verbose($"Unable to find values files at path `{valuePath}`. Chart package contains root directory with chart name, so looking for values in there.");
                    var chartRelativePath = Path.Combine(pathedPackedName, relativePath);
                    currentFiles = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, chartRelativePath).ToList();
                }

                if (!currentFiles.Any())
                {
                    errors.Add($"Unable to find file `{valuePath}` for package {pathedPackedName} v{version}");
                }

                foreach (var file in currentFiles)
                {
                    var relative = file.Substring(Path.Combine(deployment.CurrentDirectory, sanitizedPackageReferenceName).Length);
                    log.Info($"Including values file `{relative}` from package {pathedPackedName} v{version}");
                    filenames.AddRange(currentFiles);
                }
            }
            

            if (!filenames.Any() && errors.Any())
            {
                throw new CommandException(string.Join(Environment.NewLine, errors));
            }

            return filenames;
        }

        static IEnumerable<string> GenerateAndWriteChartValues(RunningDeployment deployment, ICalamariFileSystem fileSystem, ILog log, string valuesFilePaths)
        {
            return GenerateAndWritePackageValues(deployment,
                                                 fileSystem,
                                                 log,
                                                 valuesFilePaths,
                                                 null,
                                                 null);
        }

        public static string GenerateAndWriteKeyValues(RunningDeployment deployment, Dictionary<string, object> keyValues)
        {
            if (!keyValues.Any())
            {
                return null;
            }

            var fileName = Path.Combine(deployment.CurrentDirectory, "explicitVariableValues.yaml");
            File.WriteAllText(fileName, RawValuesToYamlConverter.Convert(keyValues));

            return fileName;
        }

        public static string GenerateAndWriteInlineYaml(RunningDeployment deployment, string yaml)
        {
            if (string.IsNullOrWhiteSpace(yaml))
                return null;

            var fileName = Path.Combine(deployment.CurrentDirectory, "rawYamlValues.yaml");
            File.WriteAllText(fileName, yaml);

            return fileName;
        }

        static void AddIfNotNull(List<string> filenames, string filename)
        {
            if (filename != null)
            {
                filenames.Add(filename);
            }
        }

        enum TemplateValuesSourceType
        {
            Chart,
            KeyValues,
            Package,
            InlineYaml
        }
        
        class TemplateValuesSource
        {
            public TemplateValuesSourceType Type { get; set; }
        }

        class ChartTemplateValuesSource : TemplateValuesSource
        {
            public string ValuesFilePaths { get; set; }
        }

        class InlineYamlTemplateValuesSource : TemplateValuesSource
        {
            public string Value { get; set; }
        }

        class PackageTemplateValuesSource : TemplateValuesSource
        {
            public string PackageId { get; set; }
            public string PackageName { get; set; }
            public string ValuesFilePaths { get; set; }
        }

        class KeyValuesTemplateValuesSource : TemplateValuesSource
        {
            public Dictionary<string,object> Value { get; set; }
        }
    }
}