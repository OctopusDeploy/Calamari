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
    public class HelmTemplateValueSourcesParser
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public HelmTemplateValueSourcesParser(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public IEnumerable<string> ParseAndWriteTemplateValuesFilesFromAllSources(RunningDeployment deployment)
        {
            var templateValueSources = deployment.Variables.GetRaw(SpecialVariables.Helm.TemplateValuesSources);

            if (string.IsNullOrWhiteSpace(templateValueSources))
                return Enumerable.Empty<string>();

            var parsedJsonArray = JArray.Parse(templateValueSources);

            return ParseFilenamesFromTemplateValuesArray(deployment, parsedJsonArray, true);
        }

        public IEnumerable<string> ParseTemplateValuesFilesFromDependencies(RunningDeployment deployment, bool logIncludedFiles = true)
        {
            var templateValueSources = deployment.Variables.GetRaw(SpecialVariables.Helm.TemplateValuesSources);

            if (string.IsNullOrWhiteSpace(templateValueSources))
                return Enumerable.Empty<string>();

            var parsedJsonArray = JArray.Parse(templateValueSources);

            //we are only interested in the values files in external dependencies (chart/package/git repo), so filter this array
            var relevantTypes = parsedJsonArray.Where(t =>
                                                      {
                                                          var type = (TemplateValuesSourceType)Enum.Parse(typeof(TemplateValuesSourceType), t.Value<string>(nameof(TemplateValuesSource.Type)));
                                                          return type == TemplateValuesSourceType.Chart || type == TemplateValuesSourceType.Package || type == TemplateValuesSourceType.GitRepository;
                                                      })
                                               .ToList();

            return ParseFilenamesFromTemplateValuesArray(deployment, relevantTypes, logIncludedFiles);
        }

        List<string> ParseFilenamesFromTemplateValuesArray(RunningDeployment deployment, IEnumerable<JToken> parsedJsonArray, bool logIncludedFiles)
        {
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
                        var evaluatedChartTvsValueFilePaths = deployment.Variables.Evaluate(chartTvs.ValuesFilePaths);

                        IEnumerable<string> chartFilenames;
                        var scriptSource = deployment.Variables.Get(ScriptVariables.ScriptSource);
                        switch (scriptSource)
                        {
                            case ScriptVariables.ScriptSourceOptions.Package:
                                chartFilenames = PackageValuesFileWriter.FindChartValuesFiles(deployment, fileSystem, log, evaluatedChartTvsValueFilePaths, logIncludedFiles);
                                break;
                            case ScriptVariables.ScriptSourceOptions.GitRepository:
                                chartFilenames = GitRepositoryValuesFileWriter.FindChartValuesFiles(deployment, fileSystem, log, evaluatedChartTvsValueFilePaths, logIncludedFiles);
                                break;
                            default:
                                if (scriptSource is null)
                                    throw new ArgumentNullException($"{ScriptVariables.ScriptSource} variable cannot be null");

                                throw new ArgumentException($"{scriptSource} is not a support Chart values source type");
                        }

                        if (chartFilenames != null)
                        {
                            filenames.AddRange(chartFilenames);
                        }

                        break;

                    case TemplateValuesSourceType.KeyValues:
                        var keyValuesTvs = KeyValuesTemplateValuesSource.FromJTokenWithEvaluation(json, deployment.Variables);
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
                                                                                              packageTvs.PackageName, 
                                                                                              logIncludedFiles);

                        if (packageFilenames != null)
                        {
                            filenames.AddRange(packageFilenames);
                        }

                        break;

                    case TemplateValuesSourceType.InlineYaml:
                        var inlineYamlTvs = InlineYamlTemplateValuesSource.FromJTokenWithEvaluation(json, deployment.Variables);
                        var inlineYamlFilename = InlineYamlValuesFileWriter.WriteToFile(deployment, fileSystem, inlineYamlTvs.Value, index);

                        AddIfNotNull(filenames, inlineYamlFilename);
                        break;

                    case TemplateValuesSourceType.GitRepository:
                        var gitRepTvs = json.ToObject<GitRepositoryTemplateValuesSource>();
                        var gitRepositoryFilenames = GitRepositoryValuesFileWriter.FindGitDependencyValuesFiles(deployment,
                                                                                                                fileSystem,
                                                                                                                log,
                                                                                                                gitRepTvs.GitDependencyName,
                                                                                                                gitRepTvs.ValuesFilePaths, 
                                                                                                                logIncludedFiles);

                        if (gitRepositoryFilenames != null)
                        {
                            filenames.AddRange(gitRepositoryFilenames);
                        }

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
            InlineYaml,
            GitRepository
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

            public static InlineYamlTemplateValuesSource FromJTokenWithEvaluation(JToken jToken, IVariables variables)
            {
                var tvs = jToken.ToObject<TemplateValuesSource>();
                if (tvs.Type != TemplateValuesSourceType.InlineYaml)
                {
                    throw new Exception($"Expected {TemplateValuesSourceType.InlineYaml}, but got {tvs.Type}");
                }
                
                var inlineYamlTvs = jToken.ToObject<InlineYamlTemplateValuesSource>();
                var evaluatedValue = variables.Evaluate(inlineYamlTvs.Value);

                return new InlineYamlTemplateValuesSource
                {
                    Value = evaluatedValue
                };
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

        internal class GitRepositoryTemplateValuesSource : TemplateValuesSource
        {
            public string GitDependencyName { get; set; }
            public string ValuesFilePaths { get; set; }

            public GitRepositoryTemplateValuesSource()
            {
                Type = TemplateValuesSourceType.GitRepository;
            }
        }

        internal class KeyValuesTemplateValuesSource : TemplateValuesSource
        {
            public Dictionary<string, object> Value { get; set; }

            public KeyValuesTemplateValuesSource()
            {
                Type = TemplateValuesSourceType.KeyValues;
            }
            
            public static KeyValuesTemplateValuesSource FromJTokenWithEvaluation(JToken jToken, IVariables variables)
            {
                var tvs = jToken.ToObject<TemplateValuesSource>();
                if (tvs.Type != TemplateValuesSourceType.KeyValues)
                {
                    throw new Exception($"Expected {TemplateValuesSourceType.KeyValues}, but got {tvs.Type}");
                }
                
                var keyValuesTvs = jToken.ToObject<KeyValuesTemplateValuesSource>();
                
                var evaluatedKeyValues = new Dictionary<string, object>();
                foreach (var kvp in keyValuesTvs.Value)
                {
                    var evaluatedKey = variables.Evaluate(kvp.Key);
                    var value = kvp.Value;
                            
                    var val = JToken.FromObject(kvp.Value);
                    if (val.Type == JTokenType.String)
                    {
                        value = variables.Evaluate(val.Value<string>());
                    }
                    evaluatedKeyValues.Add(evaluatedKey, value);
                }
                
                return new KeyValuesTemplateValuesSource { Value = evaluatedKeyValues };
            }
        }
    }
}