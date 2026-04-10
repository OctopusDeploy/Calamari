using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Helm;
using Newtonsoft.Json.Linq;

namespace Calamari.CommitToGit
{
    public class CommitToGitDependencyMetadataParser
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public CommitToGitDependencyMetadataParser(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public IEnumerable<string> ParseInputFilesFromDependencies(RunningDeployment deployment, bool logIncludedFiles = true)
        {
            var templateValueSources = deployment.Variables.GetRaw(SpecialVariables.Helm.TemplateValuesSources);

            if (string.IsNullOrWhiteSpace(templateValueSources))
                return Enumerable.Empty<string>();

            var parsedJsonArray = JArray.Parse(templateValueSources);

            //we are only interested in the values files in external dependencies (chart/package/git repo), so filter this array
            var relevantTypes = parsedJsonArray.Where(t =>
                                                      {
                                                          var type = (CommitToGitDependencyType)Enum.Parse(typeof(CommitToGitDependencyType), t.Value<string>(nameof(CommitToGitDependency.Type)));
                                                          return type == CommitToGitDependencyType.Package || type == CommitToGitDependencyType.GitRepository;
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
                var dependencies = jToken.ToObject<CommitToGitDependency>();
                switch (dependencies.Type)
                {
                    case CommitToGitDependencyType.Package:
                        var package = PackageDependency.FromJTokenWithEvaluation(jToken, deployment.Variables);
                        var packageFilenames = PackageValuesFileWriter.FindPackageValuesFiles(deployment,
                                                                                              fileSystem,
                                                                                              log,
                                                                                              package.DestinationSubFolder,
                                                                                              package.PackageId,
                                                                                              package.PackageName,
                                                                                              logIncludedFiles);

                        if (packageFilenames != null)
                        {
                            filenames.AddRange(packageFilenames);
                        }

                        break;

                    case CommitToGitDependencyType.Inline:
                        var inlineYamlTvs = InlineDependency.FromJTokenWithEvaluation(jToken, deployment.Variables);
                        var inlineYamlFilename = InlineYamlValuesFileWriter.WriteToFile(deployment, fileSystem, inlineYamlTvs.DestinationFilename, index);

                        AddIfNotNull(filenames, inlineYamlFilename);
                        break;

                    case CommitToGitDependencyType.GitRepository:
                        var gitRepTvs = GitRepositoryDependency.FromJTokenWithEvaluation(jToken, deployment.Variables);
                        var gitRepositoryFilenames = GitRepositoryValuesFileWriter.FindGitDependencyValuesFiles(deployment,
                                                                                                                fileSystem,
                                                                                                                log,
                                                                                                                gitRepTvs.GitDependencyName,
                                                                                                                gitRepTvs.DestinationSubFolder,
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

        public IEnumerable<string> ReferencedDependencyNames(RunningDeployment deployment)
        {
            var templateValueSources = deployment.Variables.GetRaw(SpecialVariables.Helm.TemplateValuesSources);

            if (string.IsNullOrWhiteSpace(templateValueSources))
                return Enumerable.Empty<string>();

            var parsedJsonArray = JArray.Parse(templateValueSources);

            //we are only interested in the values files in external dependencies (chart/package/git repo), so filter this array
            var relevantTypes = parsedJsonArray.Where(t =>
                                                      {
                                                          var type = (CommitToGitDependencyType)Enum.Parse(typeof(CommitToGitDependencyType), t.Value<string>(nameof(CommitToGitDependency.Type)));
                                                          return type == CommitToGitDependencyType.Package || type == CommitToGitDependencyType.GitRepository;
                                                      })
                                               .ToList();

            return ParseReferenceNames(deployment, relevantTypes);
        }

        IEnumerable<string> ParseReferenceNames(RunningDeployment deployment, IEnumerable<JToken> parsedJsonArray)
        {
            var dependencyNames = new List<string>();
            // we reverse the order of the array so that we maintain the order that sources at the top take higher precendences (i.e. are adding to the --values list later),
            // however, within a source, the file path order must be maintained (for consistency) so that later file paths take higher precendence 
            foreach (var jToken in parsedJsonArray.Select((json, index) => json))
            {
                var dependencies = jToken.ToObject<CommitToGitDependency>();
                switch (dependencies.Type)
                {
                    case CommitToGitDependencyType.Package:
                        var package = PackageDependency.FromJTokenWithEvaluation(jToken, deployment.Variables);
                        dependencyNames.Add(package.GetName());
                        break;
                    case CommitToGitDependencyType.GitRepository:
                        var gitDependency = GitRepositoryDependency.FromJTokenWithEvaluation(jToken, deployment.Variables);
                        dependencyNames.Add(gitDependency.GetName());
                        break;
                }
            }

            return dependencyNames;
        }

        static void AddIfNotNull(List<string> filenames, string filename)
        {
            if (filename != null)
            {
                filenames.Add(filename);
            }
        }
    }

    internal enum CommitToGitDependencyType
    {
        Package,
        Inline,
        GitRepository
    }

    internal class CommitToGitDependency
    {
        public CommitToGitDependencyType Type { get; set; }
    }

    internal class InlineDependency : CommitToGitDependency
    {
        public string FileContent { get; set; }
        public string DestinationFilename { get; set; }

        public InlineDependency()
        {
            Type = CommitToGitDependencyType.Inline;
        }

        public static InlineDependency FromJTokenWithEvaluation(JToken jToken, IVariables variables)
        {
            var inlineContent = jToken.ToObject<InlineDependency>();

            return new InlineDependency
            {
                FileContent = variables.Evaluate(inlineContent.FileContent),
                DestinationFilename = variables.Evaluate(inlineContent.DestinationFilename)
            };
        }
    }

    internal abstract class NamedDependency : CommitToGitDependency
    {
        public abstract string GetName();
    }

    internal class PackageDependency : NamedDependency
    {
        public string PackageId { get; set; }
        public string PackageName { get; set; }
        public string[] InputFilePaths { get; set; }
        public string DestinationSubFolder { get; set; }

        public override string GetName()
        {
            return PackageName;
        }

        public PackageDependency()
        {
            Type = CommitToGitDependencyType.Package;
        }

        public static PackageDependency FromJTokenWithEvaluation(JToken jToken, IVariables variables)
        {
            var packageDependency = jToken.ToObject<PackageDependency>();

            return new PackageDependency
            {
                PackageId = variables.Evaluate(packageDependency.PackageId),
                PackageName = variables.Evaluate(packageDependency.PackageName),
                InputFilePaths = packageDependency.InputFilePaths.Select(variables.Evaluate).ToArray(),
                DestinationSubFolder = variables.Evaluate(packageDependency.DestinationSubFolder),
            };
        }
    }

    internal class GitRepositoryDependency : NamedDependency
    {
        public string GitDependencyName { get; set; }
        public string DestinationSubFolder { get; set; }

        public override string GetName()
        {
            return GitDependencyName;
        }

        public GitRepositoryDependency()
        {
            Type = CommitToGitDependencyType.GitRepository;
        }

        public static GitRepositoryDependency FromJTokenWithEvaluation(JToken jToken, IVariables variables)
        {
            var gitRepositoryTvs = jToken.ToObject<GitRepositoryDependency>();

            return new GitRepositoryDependency
            {
                GitDependencyName = variables.Evaluate(gitRepositoryTvs.GitDependencyName),
                DestinationSubFolder = variables.Evaluate(gitRepositoryTvs.DestinationSubFolder)
            };
        }
    }
}