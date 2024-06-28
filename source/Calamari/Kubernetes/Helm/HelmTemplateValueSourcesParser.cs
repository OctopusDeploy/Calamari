using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.Helm
{
    public static class HelmTemplateValueSourcesParser
    {
        public static IEnumerable<string> ParseTemplateValuesSources(RunningDeployment deployment, ICalamariFileSystem fileSystem, ILog log)
        {
            var templateValueSources = deployment.Variables.Get(SpecialVariables.Helm.TemplateValuesSources);

            if (string.IsNullOrWhiteSpace(templateValueSources))
                return Enumerable.Empty<string>();

            var parsedJsonArray = JArray.Parse(templateValueSources);

            var filenames = new List<string>();
            // we reverse the order of the array so that we maintain the order that sources at the top take higher precendences (i.e. are adding to the --values list later),
            // however, within a source, the file path order must be maintained (for consistency) so that later file paths take higher precendence 
            foreach (var (json, index) in parsedJsonArray.Select((json, index) => (json, index)).Reverse())
            {
                var tvs = json.ToObject<TemplateValuesSource>();
                switch (tvs.Type)
                {
                    case TemplateValuesSourceType.Chart:
                        var chartTvs = json.ToObject<ChartTemplateValuesSource>();

                        IEnumerable<string> chartFilenames;
                        var scriptSource = deployment.Variables.Get(ScriptVariables.ScriptSource);
                        switch (scriptSource)
                        {
                            case ScriptVariables.ScriptSourceOptions.Package:
                                chartFilenames = PackageValuesFileWriter.FindChartValuesFiles(deployment, fileSystem, log, chartTvs.ValuesFilePaths);
                                break;
                            case ScriptVariables.ScriptSourceOptions.GitRepository:
                                chartFilenames = GitRepositoryValuesFileWriter.FindChartValuesFiles(deployment, fileSystem, log, chartTvs.ValuesFilePaths);
                                break;
                            default:
                                if (scriptSource is null)
                                    throw new ArgumentNullException($"{ScriptVariables.ScriptSource} variable cannot be null");
                                
                                throw new ArgumentException($"{scriptSource} is not a support Chart values source type");
                        }

                        filenames.AddRange(chartFilenames);
                        break;
                    case TemplateValuesSourceType.KeyValues:
                        var keyValuesTvs = json.ToObject<KeyValuesTemplateValuesSource>();
                        var keyValueFilename = KeyValuesValuesFileWriter.WriteToFile(deployment, fileSystem, keyValuesTvs.Value, index);

                        AddIfNotNull(filenames, keyValueFilename);
                        break;

                    case TemplateValuesSourceType.Package:
                        var packageTvs = json.ToObject<PackageTemplateValuesSource>();
                        var packageFilenames = PackageValuesFileWriter.FindPackageValuesFiles(deployment,
                                                                                              fileSystem,
                                                                                              log,
                                                                                              packageTvs.ValuesFilePaths,
                                                                                              packageTvs.PackageId,
                                                                                              packageTvs.PackageName);

                        filenames.AddRange(packageFilenames);
                        break;
                    case TemplateValuesSourceType.InlineYaml:
                        var inlineYamlTvs = json.ToObject<InlineYamlTemplateValuesSource>();
                        var inlineYamlFilename = InlineYamlValuesFileWriter.WriteToFile(deployment, fileSystem, inlineYamlTvs.Value, index);

                        AddIfNotNull(filenames, inlineYamlFilename);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return filenames;
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