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
        public const string KeyValuesFileName = "explicitVariableValues.yaml";
        public const string InlineYamlFileName = "rawYamlValues.yaml";

        public static IEnumerable<string> ParseTemplateValuesSources(RunningDeployment deployment, ICalamariFileSystem fileSystem, ILog log)
        {
            var templateValueSources = deployment.Variables.Get(SpecialVariables.Helm.TemplateValuesSources);

            if (string.IsNullOrWhiteSpace(templateValueSources))
                return Enumerable.Empty<string>();

            var parsedJsonArray = JArray.Parse(templateValueSources);

            var filenames = new List<string>();
            // we reverse the order of the array so that we maintain the order that sources at the top take higher precendences (i.e. are adding to the --values list later),
            // however, within a source, the file path order must be maintained (for consistency) so that later file paths take higher precendence 
            foreach (var json in parsedJsonArray.Reverse())
            {
                var tvs = json.ToObject<TemplateValuesSource>();
                switch (tvs.Type)
                {
                    case TemplateValuesSourceType.Chart:
                        var chartTvs = json.ToObject<ChartTemplateValuesSource>();
                        var chartFilenames = GenerateAndWriteChartValues(deployment, fileSystem, log, chartTvs.ValuesFilePaths);

                        filenames.AddRange(chartFilenames);
                        break;
                    case TemplateValuesSourceType.KeyValues:
                        var keyValuesTvs = json.ToObject<KeyValuesTemplateValuesSource>();
                        var keyValueFilename = GenerateAndWriteKeyValues(deployment, fileSystem, keyValuesTvs.Value);

                        AddIfNotNull(filenames, keyValueFilename);
                        break;

                    case TemplateValuesSourceType.Package:
                        var packageTvs = json.ToObject<PackageTemplateValuesSource>();
                        var packageFilenames = GenerateAndWritePackageValues(deployment,
                                                                             fileSystem,
                                                                             log,
                                                                             packageTvs.ValuesFilePaths,
                                                                             packageTvs.PackageId,
                                                                             packageTvs.PackageName);

                        filenames.AddRange(packageFilenames);
                        break;
                    case TemplateValuesSourceType.InlineYaml:
                        var inlineYamlTvs = json.ToObject<InlineYamlTemplateValuesSource>();
                        var inlineYamlFilename = GenerateAndWriteInlineYaml(deployment, fileSystem, inlineYamlTvs.Value);

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

            //if the package name is null/empty then this is the chart primary package, so we don't need to validate the packageId
            if (!string.IsNullOrEmpty(packageName))
            {
                var packageIdFromVariables = variables.Get(PackageVariables.IndexedPackageId(packageName));
                if (string.IsNullOrEmpty(packageIdFromVariables) || !packageIdFromVariables.Equals(packageId, StringComparison.CurrentCultureIgnoreCase))
                {
                    return null;
                }
            }

            var filenames = new List<string>();
            var errors = new List<string>();

            var sanitizedPackageReferenceName = PackageName.ExtractPackageNameFromPathedPackageId(fileSystem.RemoveInvalidFileNameChars(packageName));
            var valuesPaths = valuesFilePaths.Split('\r', '\n').Where(x => !string.IsNullOrWhiteSpace(x));

            foreach (var valuePath in valuesPaths)
            {
                //we get the package id here
                var pathedPackedName = PackageName.ExtractPackageNameFromPathedPackageId(variables.Get(PackageVariables.IndexedPackageId(packageName)));
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
                                                 string.Empty,
                                                 string.Empty);
        }

        public static string GenerateAndWriteKeyValues(RunningDeployment deployment, ICalamariFileSystem fileSystem, Dictionary<string, object> keyValues)
        {
            if (!keyValues.Any())
            {
                return null;
            }

            var fileName = Path.Combine(deployment.CurrentDirectory, KeyValuesFileName);
            fileSystem.WriteAllText(fileName, RawValuesToYamlConverter.Convert(keyValues));

            return fileName;
        }

        public static string GenerateAndWriteInlineYaml(RunningDeployment deployment, ICalamariFileSystem fileSystem, string yaml)
        {
            if (string.IsNullOrWhiteSpace(yaml))
                return null;

            var fileName = Path.Combine(deployment.CurrentDirectory, "rawYamlValues.yaml");
            fileSystem.WriteAllText(fileName, yaml);

            return fileName;
        }

        static void AddIfNotNull(List<string> filenames, string filename)
        {
            if (filename != null)
            {
                filenames.Add(filename);
            }
        }

        internal enum TemplateValuesSourceType
        {
            Chart,
            KeyValues,
            Package,
            InlineYaml
        }

        internal class TemplateValuesSource
        {
            public TemplateValuesSourceType Type { get; set; }
        }

        internal class ChartTemplateValuesSource : TemplateValuesSource
        {
            public string ValuesFilePaths { get; set; }

            public ChartTemplateValuesSource()
            {
                Type = TemplateValuesSourceType.Chart;
            }
        }

        internal class InlineYamlTemplateValuesSource : TemplateValuesSource
        {
            public string Value { get; set; }

            public InlineYamlTemplateValuesSource()
            {
                Type = TemplateValuesSourceType.InlineYaml;
            }
        }

        internal class PackageTemplateValuesSource : TemplateValuesSource
        {
            public string PackageId { get; set; }
            public string PackageName { get; set; }
            public string ValuesFilePaths { get; set; }

            public PackageTemplateValuesSource()
            {
                Type = TemplateValuesSourceType.Package;
            }
        }

        internal class KeyValuesTemplateValuesSource : TemplateValuesSource
        {
            public Dictionary<string, object> Value { get; set; }

            public KeyValuesTemplateValuesSource()
            {
                Type = TemplateValuesSourceType.KeyValues;
            }
        }
    }
}