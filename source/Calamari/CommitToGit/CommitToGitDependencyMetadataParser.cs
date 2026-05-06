using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
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

        public IEnumerable<string> ReferencedDependencyNames(RunningDeployment deployment)
        {
            var templateValueSources = deployment.Variables.GetRaw(Deployment.SpecialVariables.Action.Git.InputFileSources);

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
                        dependencyNames.Add(fileSystem.RemoveInvalidFileNameChars(package.GetName()));
                        break;
                    case CommitToGitDependencyType.GitRepository:
                        var gitDependency = GitRepositoryDependency.FromJTokenWithEvaluation(jToken, deployment.Variables);
                        dependencyNames.Add(fileSystem.RemoveInvalidFileNameChars(gitDependency.GetName()));
                        break;
                }
            }

            return dependencyNames;
        }

        public IEnumerable<PackageDependency> GetPackageDependenciesForCopying(RunningDeployment deployment)
        {
            return GetDependenciesOfType<PackageDependency>(deployment, CommitToGitDependencyType.Package,
                jToken => PackageDependency.FromJTokenWithEvaluation(jToken, deployment.Variables));
        }

        public IEnumerable<GitRepositoryDependency> GetGitRepositoryDependenciesForCopying(RunningDeployment deployment)
        {
            return GetDependenciesOfType<GitRepositoryDependency>(deployment, CommitToGitDependencyType.GitRepository,
                jToken => GitRepositoryDependency.FromJTokenWithEvaluation(jToken, deployment.Variables));
        }

        IEnumerable<T> GetDependenciesOfType<T>(RunningDeployment deployment, CommitToGitDependencyType dependencyType, Func<JToken, T> factory)
        {
            var templateValueSources = deployment.Variables.GetRaw(Deployment.SpecialVariables.Action.Git.InputFileSources);
            if (string.IsNullOrWhiteSpace(templateValueSources))
                return Enumerable.Empty<T>();

            var parsedJsonArray = JArray.Parse(templateValueSources);
            return parsedJsonArray
                .Where(t => (CommitToGitDependencyType)Enum.Parse(typeof(CommitToGitDependencyType), t.Value<string>(nameof(CommitToGitDependency.Type))) == dependencyType)
                .Select(factory)
                .ToList();
        }

    }

    public enum CommitToGitDependencyType
    {
        Package,
        GitRepository
    }

    public class CommitToGitDependency
    {
        public CommitToGitDependencyType Type { get; set; }
    }

    public abstract class NamedDependency : CommitToGitDependency
    {
        public abstract string GetName();
    }

    public class PackageDependency : NamedDependency
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
                InputFilePaths = packageDependency.InputFilePaths?.Select(p => variables.Evaluate(p)).ToArray(),
                DestinationSubFolder = variables.Evaluate(packageDependency.DestinationSubFolder),
            };
        }
    }

    public class GitRepositoryDependency : NamedDependency
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