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
            foreach (var (jToken, index) in parsedJsonArray.Select((json, index) => (json, index)).Reverse())
            {
                var tvs = jToken.ToObject<TemplateValuesSource>();
                switch (tvs.Type)
                {
                    case TemplateValuesSourceType.Chart:
                        var chartTvs = ChartTemplateValuesSource.FromJTokenWithEvaluation(jToken, deployment.Variables);
                        
                        IEnumerable<string> chartFilenames;
                        var scriptSource = deployment.Variables.Get(ScriptVariables.ScriptSource);
                        switch (scriptSource)
                        {
                            case ScriptVariables.ScriptSourceOptions.Package:
                                chartFilenames = PackageValuesFileWriter.FindChartValuesFiles(deployment, fileSystem, log, chartTvs.ValuesFilePaths, logIncludedFiles);
                                break;
                            case ScriptVariables.ScriptSourceOptions.GitRepository:
                                chartFilenames = GitRepositoryValuesFileWriter.FindChartValuesFiles(deployment, fileSystem, log, chartTvs.ValuesFilePaths, logIncludedFiles);
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
                        var keyValuesTvs = KeyValuesTemplateValuesSource.FromJTokenWithEvaluation(jToken, deployment.Variables);
                        var keyValueFilename = KeyValuesValuesFileWriter.WriteToFile(deployment, fileSystem, keyValuesTvs.Value, index);

                        AddIfNotNull(filenames, keyValueFilename);
                        break;

                    case TemplateValuesSourceType.Package:
                        var packageTvs = PackageTemplateValuesSource.FromJTokenWithEvaluation(jToken, deployment.Variables);
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
                        var inlineYamlTvs = InlineYamlTemplateValuesSource.FromJTokenWithEvaluation(jToken, deployment.Variables);
                        var inlineYamlFilename = InlineYamlValuesFileWriter.WriteToFile(deployment, fileSystem, inlineYamlTvs.Value, index);

                        AddIfNotNull(filenames, inlineYamlFilename);
                        break;

                    case TemplateValuesSourceType.GitRepository:
                        var gitRepTvs = GitRepositoryTemplateValuesSource.FromJTokenWithEvaluation(jToken, deployment.Variables);
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

            public static ChartTemplateValuesSource FromJTokenWithEvaluation(JToken jToken, IVariables variables)
            {
                var tvs = jToken.ToObject<TemplateValuesSource>();
                if (tvs.Type != TemplateValuesSourceType.Chart)
                {
                    throw new Exception($"Expected {TemplateValuesSourceType.Chart}, but got {tvs.Type}");
                }
                
                var chartTvs = jToken.ToObject<ChartTemplateValuesSource>();

                return new ChartTemplateValuesSource
                {
                    ValuesFilePaths = variables.Evaluate(chartTvs.ValuesFilePaths)
                };
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

                return new InlineYamlTemplateValuesSource
                {
                    Value = variables.Evaluate(inlineYamlTvs.Value)
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

            public static PackageTemplateValuesSource FromJTokenWithEvaluation(JToken jToken, IVariables variables)
            {
                var tvs = jToken.ToObject<TemplateValuesSource>();
                if (tvs.Type != TemplateValuesSourceType.Package)
                {
                    throw new Exception($"Expected {TemplateValuesSourceType.Package}, but got {tvs.Type}");
                }
                
                var packageTvs = jToken.ToObject<PackageTemplateValuesSource>();

                return new PackageTemplateValuesSource
                {
                    PackageId = variables.Evaluate(packageTvs.PackageId),
                    PackageName = variables.Evaluate(packageTvs.PackageName),
                    ValuesFilePaths = variables.Evaluate(packageTvs.ValuesFilePaths)
                };
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

            public static GitRepositoryTemplateValuesSource FromJTokenWithEvaluation(JToken jToken, IVariables variables)
            {
                var tvs = jToken.ToObject<TemplateValuesSource>();
                if (tvs.Type != TemplateValuesSourceType.GitRepository)
                {
                    throw new Exception($"Expected {TemplateValuesSourceType.GitRepository}, but got {tvs.Type}");
                }
                
                var gitRepositoryTvs = jToken.ToObject<GitRepositoryTemplateValuesSource>();

                return new GitRepositoryTemplateValuesSource
                {
                    GitDependencyName = variables.Evaluate(gitRepositoryTvs.GitDependencyName),
                    ValuesFilePaths = variables.Evaluate(gitRepositoryTvs.ValuesFilePaths)
                };
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