using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.ArgoCD.Commands;
using Calamari.ArgoCD.Commands.Executors;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using LibGit2Sharp;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands
{
    public class CommitToGitRepositoryCommandTests
    {
        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        InMemoryLog log;
        string tempDirectory;
        string StagingDirectory => Path.Combine(tempDirectory, "staging");
        string PackageDirectory => Path.Combine(StagingDirectory, CommitToGitRepositoryCommand.PackageDirectoryName);
        string OriginPath => Path.Combine(tempDirectory, "origin");

        string branchName = "devBranch";
        Repository BareOrigin;
        
        [SetUp]
        public void Init()
        {
            log = new InMemoryLog();
            tempDirectory = fileSystem.CreateTemporaryDirectory();
            Directory.CreateDirectory(PackageDirectory);
            
            Directory.CreateDirectory(OriginPath);
            Repository.Init(OriginPath, isBare: true);
            
            BareOrigin = new Repository(OriginPath);
            CreateDevBranchIn(OriginPath);
        }

        void CreateDevBranchIn(string originPath)
        {
            var signature = new Signature("Your Name", "your.email@example.com", DateTimeOffset.Now);
            
            var repository = new Repository(OriginPath);
            repository.Refs.UpdateTarget("HEAD", "refs/heads/master");
            var tree = repository.ObjectDatabase.CreateTree(new TreeDefinition());
            var commit = repository.ObjectDatabase.CreateCommit(
                                                                signature,
                                                                signature,
                                                                "InitializeRepo",
                                                                tree,
                                                                Array.Empty<Commit>(),
                                                                false);
            repository.CreateBranch(branchName, commit);
        }
                
        [TearDown]
        public void Cleanup()
        {
            fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);
        }
        
        
        [Test]
        public void ExecuteCopiesFilesFromPackageIntoRepo()
        {
            //this all needs to go into a zip file somehow
            var nestedDirectory = Path.Combine(PackageDirectory, "nested");
            Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(Path.Combine(PackageDirectory,"first.yaml"), "firstContent");
            File.WriteAllText(Path.Combine(nestedDirectory, "second.yaml"), "secondContent");
            
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory,
                [SpecialVariables.CustomResourceYamlFileName] = "./*\nnested/*",
                [SpecialVariables.Git.Folder] = "",
                [SpecialVariables.Git.Password] = "password",
                [SpecialVariables.Git.Username] = "username",
                [SpecialVariables.Git.Url] = OriginPath,
                [SpecialVariables.Git.BranchName] = branchName,
            };
            
            var extractPackage = new ExtractPackage(new CombinedPackageExtractor(log, fileSystem, variables, new CommandLineRunner(log, variables)), fileSystem, variables, log);
            var fileSubstituter = new SubstituteInFiles(log, fileSystem, new FileSubstituter(log, fileSystem), variables);
            var command = new CommitToGitRepositoryCommand(log, variables, fileSystem,extractPackage, fileSubstituter);
            command.Execute(new string[] { "package=blah.zip" });
            
            var resultPath = Path.Combine(tempDirectory, "result");
            Repository.Clone(OriginPath, resultPath);
            var resultRepo = new Repository(resultPath);
            LibGit2Sharp.Commands.Checkout(resultRepo, $"origin/{branchName}");
            var resultFirstContent = File.ReadAllText(Path.Combine(resultPath, "first.yaml"));
            var resultNestedContent = File.ReadAllText(Path.Combine(resultPath, "nested", "second.yaml"));
            
            resultFirstContent.Should().Be("firstContent");
            resultNestedContent.Should().Be("secondContent");
        }
        
        
        
    }
}