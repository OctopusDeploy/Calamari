using System;
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
        var proofFile = Path.Combine(executionDirectory, "script_ran.txt");

        variables.AddRange([
            new CalamariExecutionVariable(ScriptVariables.ScriptBody, $"touch {proofFile}", false),
            new CalamariExecutionVariable(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString(), false),
        ]);

        var absPathToVariables = Path.Combine(executionDirectory, variableFileName);
        File.WriteAllBytes(absPathToVariables, AesEncryption.ForServerVariables(variablePassword).Encrypt(variables.ToJsonString()));

        var result = Program.Main(["commit-to-git", "--variables", $"{absPathToVariables}", "--variablesPassword", $"{variablePassword}"]);
        result.Should().Be(0);
        File.Exists(proofFile).Should().BeTrue("the transform script should have run");
    }

    [Test]
    [Category("Nix")]
    public void CommitToGitRunsInlineScriptWithPackageDependency()
    {
        var proofFile = Path.Combine(executionDirectory, "script_ran.txt");

        const string packageReferenceName = "my-scripts";
        var zipPath = Path.Combine(executionDirectory, $"{packageReferenceName}.1.0.0.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("helper.sh");
            using var writer = new StreamWriter(entry.Open());
            // The dependency creates the proof file, proving it was extracted and invoked
            writer.Write($"touch {proofFile}");
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
        File.Exists(proofFile).Should().BeTrue("the transform script should have invoked the package dependency");
    }

    [Test]
    [Category("Nix")]
    public void CommitToGitRunsPackageBasedScriptWithNoDependencies()
    {
        var proofFile = Path.Combine(executionDirectory, "script_ran.txt");

        var zipPath = Path.Combine(executionDirectory, "transform-script.1.0.0.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("script.sh");
            using var writer = new StreamWriter(entry.Open());
            writer.Write($"touch {proofFile}");
        }

        variables.AddRange([
            new CalamariExecutionVariable(ScriptVariables.ScriptFileName, "script.sh", false),
        ]);

        var absPathToVariables = Path.Combine(executionDirectory, variableFileName);
        File.WriteAllBytes(absPathToVariables, AesEncryption.ForServerVariables(variablePassword).Encrypt(variables.ToJsonString()));

        var result = Program.Main(["commit-to-git", "--package", zipPath, "--variables", absPathToVariables, "--variablesPassword", variablePassword]);
        result.Should().Be(0);
        File.Exists(proofFile).Should().BeTrue("the transform script should have run");
    }

    [Test]
    [Category("Nix")]
    public void CommitToGitRunsPackageBasedScriptWithScriptParameters()
    {
        var proofFile = Path.Combine(executionDirectory, "script_ran.txt");

        var zipPath = Path.Combine(executionDirectory, "transform-script.1.0.0.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("script.sh");
            using var writer = new StreamWriter(entry.Open());
            // Exits non-zero if the expected argument was not received, otherwise creates proof
            writer.Write($"if [ \"$1\" != \"expected-arg\" ]; then exit 1; fi{Environment.NewLine}touch {proofFile}");
        }

        variables.AddRange([
            new CalamariExecutionVariable(ScriptVariables.ScriptFileName, "script.sh", false),
        ]);

        var absPathToVariables = Path.Combine(executionDirectory, variableFileName);
        File.WriteAllBytes(absPathToVariables, AesEncryption.ForServerVariables(variablePassword).Encrypt(variables.ToJsonString()));

        var result = Program.Main(["commit-to-git", "--package", zipPath, "--scriptParameters", "expected-arg", "--variables", absPathToVariables, "--variablesPassword", variablePassword]);
        result.Should().Be(0);
        File.Exists(proofFile).Should().BeTrue("the transform script should have run with the expected argument");
    }

    [Test]
    [Category("Nix")]
    public void CanCopyPackageFilesIntoGitRepository()
    {
        const string packageReferenceName = "my-configs";
        const string destinationPath = "output-dir";

        var zipPath = Path.Combine(executionDirectory, $"{packageReferenceName}.1.0.0.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("configs/settings.json");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("{\"setting\": \"value\"}");
        }

        var templateValueSources = $"[{{\"Type\":\"Package\",\"PackageId\":\"{packageReferenceName}\",\"PackageName\":\"{packageReferenceName}\",\"InputFilePaths\":[\"configs/**\"],\"DestinationSubFolder\":\"\"}}]";

        variables.AddRange([
            // No-op transform script — required for the command to execute successfully
            new CalamariExecutionVariable(ScriptVariables.ScriptBody, "exit 0", false),
            new CalamariExecutionVariable(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString(), false),
            // Package to copy into the repository, declared via TemplateValuesSources
            new CalamariExecutionVariable(PackageVariables.IndexedPackageId(packageReferenceName), packageReferenceName, false),
            new CalamariExecutionVariable(PackageVariables.IndexedOriginalPath(packageReferenceName), zipPath, false),
            new CalamariExecutionVariable(SpecialVariables.Helm.TemplateValuesSources, templateValueSources, false),
            // Override the destination path set in setUp
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.DestinationPath, destinationPath, false),
        ]);

        var absPathToVariables = Path.Combine(executionDirectory, variableFileName);
        File.WriteAllBytes(absPathToVariables, AesEncryption.ForServerVariables(variablePassword).Encrypt(variables.ToJsonString()));

        var result = Program.Main(["commit-to-git", "--variables", absPathToVariables, "--variablesPassword", variablePassword]);
        result.Should().Be(0);

        using var repo = new Repository(OriginPath);
        var tip = repo.Branches[targetBranchFriendlyName].Tip;
        tip.Tree[$"{destinationPath}/{packageReferenceName}/configs/settings.json"]
           .Should().NotBeNull("the package files should have been copied into the repository under the destination path");
    }

    [Test]
    public void SubstitutesNonSensitiveVariablesIntoAnExpandedPackageThenCopiesToGitReposiotory()
    {
    }
}