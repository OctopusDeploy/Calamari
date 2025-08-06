using System.IO;
using System.Threading.Tasks;
using Calamari.ArgoCD.Commands;
using Calamari.ArgoCD.Commands.Executors;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using LibGit2Sharp;
using NSubstitute;
using NuGet.Packaging.Core;
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
        string GitOriginPath => Path.Combine(tempDirectory, "origin");

        [SetUp]
        public void Init()
        {
            log = new InMemoryLog();
            tempDirectory = fileSystem.CreateTemporaryDirectory();
            Repository.Init(GitOriginPath);
        }

        [Test]
        public async Task ExecuteCopiesFilesFromPackageIntoRepo()
        {
            File.WriteAllText(Path.Combine(PackageDirectory,"first.yaml"), "firstContent");
            File.WriteAllText(Path.Combine(PackageDirectory, "nested", "second.yaml"), "secondContent");
            
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory,
                [SpecialVariables.CustomResourceYamlFileName] = "./*\nnested/*",
                [SpecialVariables.Git.Folder] = "",
                [SpecialVariables.Git.Password] = "password",
                [SpecialVariables.Git.Username] = "username",
                [SpecialVariables.Git.Url] = GitOriginPath,
            };
            var runningDeployment = new RunningDeployment(PackageDirectory, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = StagingDirectory;
            
            var executor = new UpdateGitFromTemplatesExecutor(fileSystem, log);
            
            await executor.Execute(runningDeployment, PackageDirectory);
        }
        
        
        
    }
}