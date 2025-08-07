using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Calamari.ArgoCD.Commands;
using Calamari.ArgoCD.Commands.Executors;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using LibGit2Sharp;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands.Executors
{
    [TestFixture]
    public class UpdateGitFromTemplatesExecutorTests
    {
        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        InMemoryLog log;
        string tempDirectory;
        string StagingDirectory => Path.Combine(tempDirectory, "staging");
        string PackageDirectory => Path.Combine(StagingDirectory, UpdateGitRepoFromTemplates.PackageDirectoryName);
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
            //to create a commit in a bare repo you have to push it into the repo - go figure.
            var tmpClone = Path.Combine(tempDirectory, "clone");
            Repository.Clone(originPath, tmpClone);
            var repo = new Repository(tmpClone); 
            
            var author = new Signature("Your Name", "your.email@example.com", DateTimeOffset.Now);
            var committer = author; // Often the same for simple commits

            // Create CommitOptions and set AllowEmptyCommit to true
            var commitOptions = new CommitOptions
            {
                AllowEmptyCommit = true
            };

            var readmeFilename = "readme.txt";
            var readmeFile = Path.Combine(tmpClone, readmeFilename);
            File.WriteAllText(readmeFile,"readme");
            repo.Index.Add(readmeFilename);

            // Commit the empty changes
            repo.Commit("Your empty commit message", author, committer, commitOptions);
            var localBranch = repo.CreateBranch(branchName);
            Remote remote = repo.Network.Remotes["origin"];
            repo.Branches.Update(localBranch, 
                                 branch => branch.Remote = remote.Name,
                                 branch => branch.UpstreamBranch = localBranch.CanonicalName);
            
            repo.Network.Push(localBranch);
        }
        
        [TearDown]
        public void Cleanup()
        {
            fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);
        }

        [Test]
        public async Task ExecuteCopiesFilesFromPackageIntoRepo()
        {
            File.WriteAllText(Path.Combine(PackageDirectory,"first.yaml"), "firstContent");
            var nestedDirectory = Path.Combine(PackageDirectory, "nested");
            Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(Path.Combine(nestedDirectory, "second.yaml"), "secondContent");
            
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory,
                [SpecialVariables.CustomResourceYamlFileName] = "./*\nnested/*",
                [SpecialVariables.Git.Folder] = "",
                //[SpecialVariables.Git.Password] = "password",
                //[SpecialVariables.Git.Username] = "username",
                [SpecialVariables.Git.Url] = OriginPath,
                [SpecialVariables.Git.BranchName] = branchName,
            };
            var runningDeployment = new RunningDeployment(PackageDirectory, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = StagingDirectory;
            
            var executor = new UpdateGitFromTemplatesExecutor(fileSystem, log);
            
            await executor.Execute(runningDeployment, PackageDirectory);
        }
    }
}