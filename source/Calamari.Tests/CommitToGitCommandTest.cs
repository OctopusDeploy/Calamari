using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using Calamari.Tests.ArgoCD.Git;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using Google.Protobuf.Reflection;
using LibGit2Sharp;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Calamari.Tests;

[TestFixture]
public class CommitToGitCommandTest
{
    readonly ILog log = new InMemoryLog();
    readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
    readonly string variablePassword = "password";
    readonly string variableFileName = "variables.json";
    string executionDirectory;

    string OriginPath => Path.Combine(executionDirectory, "origin");
    string RepoUrl => OriginPath;
    Repository originRepo;
    const string targetBranchFriendlyName = "devBranch";
    readonly GitBranchName targetBranchName = GitBranchName.CreateFromFriendlyName(targetBranchFriendlyName);

    CalamariExecutionVariableCollection variables = new CalamariExecutionVariableCollection();

    [SetUp]
    public void setUp()
    {
        executionDirectory = fileSystem.CreateTemporaryDirectory();

        originRepo = RepositoryHelpers.CreateBareRepository(OriginPath);
        RepositoryHelpers.CreateBranchIn(targetBranchName, OriginPath);

        // Add git-repository data to the variables.
        variables.AddRange([
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.Url, OriginPath, false),
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.Username, "", false),
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.Password, "", true),
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.Reference, targetBranchFriendlyName, false),
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.DestinationPath, "arbitrarySubPath", false),
            new CalamariExecutionVariable(SpecialVariables.Git.PullRequest.Create, "false", false),
            new CalamariExecutionVariable(SpecialVariables.Git.CommitMessageSummary, "Git Commit Summary", false),
        ]);
        
        Directory.SetCurrentDirectory(executionDirectory);
    }

    [Test]
    [Category("Nix")]
    public void CommitToGitRunsScriptWithNoDependencies()
    {
        variables.AddRange([
            new CalamariExecutionVariable(ScriptVariables.ScriptBody, "exit 0", false),
            new CalamariExecutionVariable(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString(), false),
        ]);
        
        var absPathToVariables = Path.Combine(executionDirectory, variableFileName);
        File.WriteAllBytes(absPathToVariables, AesEncryption.ForServerVariables(variablePassword).Encrypt(variables.ToJsonString()));

        var result = Program.Main(["commit-to-git", "--variables", $"{absPathToVariables}", "--variablesPassword", $"{variablePassword}"]);
        result.Should().Be(0);
    }

    [Test]
    [Category("Nix")]
    public void CommitToGitRunsScriptWithPackageDependency()
    {
        const string packageReferenceName = "my-scripts";
        var zipPath = Path.Combine(executionDirectory, $"{packageReferenceName}.1.0.0.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("helper.sh");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("exit 0");
        }

        variables.AddRange([
            new CalamariExecutionVariable(ScriptVariables.ScriptBody, $"bash {packageReferenceName}/helper.sh", false),
            new CalamariExecutionVariable(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString(), false),
            new CalamariExecutionVariable(PackageVariables.IndexedPackageId(packageReferenceName), packageReferenceName, false),
            new CalamariExecutionVariable(PackageVariables.IndexedOriginalPath(packageReferenceName), zipPath, false),
        ]);
        var absPathToVariables = Path.Combine(executionDirectory, variableFileName);
        File.WriteAllBytes(absPathToVariables, AesEncryption.ForServerVariables(variablePassword).Encrypt(variables.ToJsonString()));

        var result = Program.Main(["commit-to-git", "--variables", absPathToVariables, "--variablesPassword", variablePassword]);
        result.Should().Be(0);
    }

    // Not sure if this is useful test atm.
    // [Test]
    // public void CommitToGitCanBeCreated()
    // {
    //     var absPathToVariables = Path.Combine(executionDirectory, variableFileName);
    //     File.WriteAllBytes(absPathToVariables, AesEncryption.ForServerVariables(variablePassword).Encrypt(variables.ToJsonString()));
    //
    //     var result = Program.Main(["commit-to-git", $"variables={absPathToVariables}", $"variablesPassword={variablePassword}"]);
    //     result.Should().Be(0);
    // }

    [Test]
    public void CanCopyPackageFilesIntoGitRepository()
    {
        const string packageReferenceName = "my-scripts";
        var zipPath = Path.Combine(executionDirectory, $"{packageReferenceName}.1.0.0.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("helper.sh");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("exit 0");
        }

        var variables = new CalamariExecutionVariableCollection
        {
            new CalamariExecutionVariable(ScriptVariables.ScriptBody, $"bash {packageReferenceName}/helper.sh", false),
            new CalamariExecutionVariable(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString(), false),
            new CalamariExecutionVariable(PackageVariables.IndexedPackageId(packageReferenceName), packageReferenceName, false),
            new CalamariExecutionVariable(PackageVariables.IndexedOriginalPath(packageReferenceName), zipPath, false),

            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.Url, OriginPath, false),
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.Username, "", false),
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.Password, "", true),
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.Reference, targetBranchFriendlyName, false),
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.DestinationPath, "arbitrarySubPath", false),
        };

        var absPathToVariables = Path.Combine(executionDirectory, variableFileName);
        File.WriteAllBytes(absPathToVariables, AesEncryption.ForServerVariables(variablePassword).Encrypt(variables.ToJsonString()));

        var result = Program.Main(["commit-to-git", $"variables={absPathToVariables}", $"variablesPassword={variablePassword}"]);
        result.Should().Be(0);
    }
    
    [Test]
    public void SubstitutesNonSensitiveVariablesIntoAnExpandedPackageThenCopiesToGitReposiotory()
    {
    }
}

