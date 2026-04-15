using System;
using System.IO;
using System.IO.Compression;
using Calamari.ArgoCD.Git;
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
using LibGit2Sharp;
using NUnit.Framework;

namespace Calamari.Tests;

[TestFixture]
public class CommitToGitCommandTest
{
    readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
    readonly string variablePassword = "password";
    readonly string variableFileName = "variables.json";
    string executionDirectory;

    string OriginPath => Path.Combine(executionDirectory, "origin");
    const string targetBranchFriendlyName = "devBranch";
    readonly GitBranchName targetBranchName = GitBranchName.CreateFromFriendlyName(targetBranchFriendlyName);

    CalamariExecutionVariableCollection variables = new();

    [SetUp]
    public void setUp()
    {
        executionDirectory = fileSystem.CreateTemporaryDirectory();

        RepositoryHelpers.CreateBareRepository(OriginPath);
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
    public void CommitToGitRunsScriptWithNoDependenciesToCreateFileWhichIsCommitedToRepository()
    {
        variables.AddRange([
            new CalamariExecutionVariable(ScriptVariables.ScriptBody, "touch \"$(get_octopusvariable 'Octopus.Calamari.Git.RepositoryPath')/proof.txt\"", false),
            new CalamariExecutionVariable(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString(), false),
        ]);

        RunCommitToGit().Should().Be(0);
        GetCommittedFileContent("proof.txt").Should().NotBeNull("the transform script should have created and committed the file to the repository");
    }

    [Test]
    [Category("Nix")]
    public void CommitToGitRunsInlineScriptWithPackageDependencyToCreateFileWhichIsCommitedToRepository()
    {
        const string packageReferenceName = "my-scripts";

        var zipPath = CreateZipWithEntry(packageReferenceName, "helper.sh", "touch \"$(get_octopusvariable 'Octopus.Calamari.Git.RepositoryPath')/proof.txt\"");

        variables.AddRange([
            new CalamariExecutionVariable(ScriptVariables.ScriptBody, $"bash {packageReferenceName}/helper.sh", false),
            new CalamariExecutionVariable(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString(), false),
            new CalamariExecutionVariable(PackageVariables.IndexedPackageId(packageReferenceName), packageReferenceName, false),
            new CalamariExecutionVariable(PackageVariables.IndexedOriginalPath(packageReferenceName), zipPath, false),
        ]);

        RunCommitToGit().Should().Be(0);
        GetCommittedFileContent("proof.txt").Should().NotBeNull("the helper script should have created and committed the file to the repository");
    }

    [Test]
    [Category("Nix")]
    public void CommitToGitRunsPackageBasedScriptWithNoDependenciesToCreateFileWhichIsCommitedToRepository()
    {
        var zipPath = CreateZipWithEntry("transform-script", "script.sh", "touch \"$(get_octopusvariable 'Octopus.Calamari.Git.RepositoryPath')/proof.txt\"");

        variables.AddRange([
            new CalamariExecutionVariable(ScriptVariables.ScriptFileName, "script.sh", false),
        ]);

        RunCommitToGit("--package", zipPath).Should().Be(0);
        GetCommittedFileContent("proof.txt").Should().NotBeNull("the package-based script should have created and committed the file to the repository");
    }

    [Test]
    [Category("Nix")]
    public void CommitToGitRunsPackageBasedScriptWithScriptParameters()
    {
        var proofFile = Path.Combine(executionDirectory, "script_ran.txt");
        var zipPath = CreateZipWithEntry("transform-script", "script.sh",
            $"if [ \"$1\" != \"expected-arg\" ]; then exit 1; fi{Environment.NewLine}touch {proofFile}");

        variables.AddRange([
            new CalamariExecutionVariable(ScriptVariables.ScriptFileName, "script.sh", false),
        ]);

        RunCommitToGit("--package", zipPath, "--scriptParameters", "expected-arg").Should().Be(0);
        File.Exists(proofFile).Should().BeTrue("the transform script should have run with the expected argument");
    }

    [Test]
    [Category("PlatformAgnostic")]
    public void CanCopyPackageFilesIntoGitRepository()
    {
        const string packageReferenceName = "my-configs";
        const string destinationPath = "output-dir";

        var zipPath = CreateZipWithEntry(packageReferenceName, "configs/settings.json", "{\"setting\": \"value\"}");
        AddInputPackageVariables(packageReferenceName, zipPath, destinationPath);

        RunCommitToGit().Should().Be(0);

        GetCommittedFileContent($"{destinationPath}/{packageReferenceName}/configs/settings.json")
            .Should().NotBeNull("the package files should have been copied into the repository under the destination path");
    }

    [Test]
    [Category("PlatformAgnostic")]
    public void SubstitutesNonSensitiveVariablesIntoAnExpandedPackageThenCopiesToGitRepository()
    {
        const string packageReferenceName = "my-configs";
        const string destinationPath = "output-dir";

        var zipPath = CreateZipWithEntry(packageReferenceName, "configs/settings.json", "{\"setting\": \"#{MyVar}\"}");
        AddInputPackageVariables(packageReferenceName, zipPath, destinationPath);

        variables.AddRange([
            new CalamariExecutionVariable(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.SubstituteInFiles, false),
            new CalamariExecutionVariable(PackageVariables.SubstituteInFilesTargets, $"{packageReferenceName}/configs/settings.json", false),
            // Non-sensitive variable to be substituted into the package file
            new CalamariExecutionVariable("MyVar", "production-value", false),
        ]);

        RunCommitToGit().Should().Be(0);

        var content = GetCommittedFileContent($"{destinationPath}/{packageReferenceName}/configs/settings.json");
        content.Should().NotBeNull("the package file should have been copied into the repository");
        content.Should().Contain("production-value", "the non-sensitive variable should have been substituted");
        content.Should().NotContain("#{MyVar}", "the Octostache template should have been replaced");
    }

    [Test]
    [Category("PlatformAgnostic")]
    public void DoesNotSubstituteSensitiveVariablesIntoAnExpandedPackageThenCopiesToGitRepository()
    {
        const string packageReferenceName = "my-configs";
        const string destinationPath = "output-dir";

        var zipPath = CreateZipWithEntry(packageReferenceName, "configs/settings.json", "{\"setting\": \"#{MySecret}\"}");
        AddInputPackageVariables(packageReferenceName, zipPath, destinationPath);

        variables.AddRange([
            new CalamariExecutionVariable(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.SubstituteInFiles, false),
            new CalamariExecutionVariable(PackageVariables.SubstituteInFilesTargets, $"{packageReferenceName}/configs/settings.json", false),
            // Sensitive variable — must NOT be substituted into repository files
            new CalamariExecutionVariable("MySecret", "super-secret-value", true),
        ]);
        
        RunCommitToGit().Should().NotBe(0);

        var content = GetCommittedFileContent($"{destinationPath}/{packageReferenceName}/configs/settings.json");
        content.Should().BeNull("the package file should not have been copied and commited into the repository");
    }

    // --- Helpers ---

    int RunCommitToGit(params string[] extraArgs)
    {
        var absPathToVariables = Path.Combine(executionDirectory, variableFileName);
        File.WriteAllBytes(absPathToVariables, AesEncryption.ForServerVariables(variablePassword).Encrypt(variables.ToJsonString()));
        return Program.Main(["commit-to-git", "--variables", absPathToVariables, "--variablesPassword", variablePassword, ..extraArgs]);
    }

    string CreateZipWithEntry(string packageName, string entryPath, string content)
    {
        var zipPath = Path.Combine(executionDirectory, $"{packageName}.1.0.0.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry(entryPath);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
        return zipPath;
    }

    void AddInputPackageVariables(string packageReferenceName, string zipPath, string destinationPath)
    {
        var templateValueSources = $"[{{\"Type\":\"Package\",\"PackageId\":\"{packageReferenceName}\",\"PackageName\":\"{packageReferenceName}\",\"InputFilePaths\":[\"configs/**\"],\"DestinationSubFolder\":\"\"}}]";
        variables.AddRange([
            // Package to copy into the repository, declared via TemplateValuesSources
            new CalamariExecutionVariable(PackageVariables.IndexedPackageId(packageReferenceName), packageReferenceName, false),
            new CalamariExecutionVariable(PackageVariables.IndexedOriginalPath(packageReferenceName), zipPath, false),
            new CalamariExecutionVariable(SpecialVariables.Helm.TemplateValuesSources, templateValueSources, false),
            // Override the destination path set in setUp
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.DestinationPath, destinationPath, false),
        ]);
    }

    string GetCommittedFileContent(string repoFilePath)
    {
        using var repo = new Repository(OriginPath);
        var entry = repo.Branches[targetBranchFriendlyName].Tip.Tree[repoFilePath];
        return entry != null ? ((Blob)entry.Target).GetContentText() : null;
    }
}
