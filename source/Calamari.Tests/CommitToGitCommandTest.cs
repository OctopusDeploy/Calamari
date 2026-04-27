using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Calamari.ArgoCD.Git;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
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

    CalamariExecutionVariableCollection variables;

    [SetUp]
    public void setUp()
    {
        executionDirectory = fileSystem.CreateTemporaryDirectory();

        RepositoryHelpers.CreateBareRepository(OriginPath);
        RepositoryHelpers.CreateBranchIn(targetBranchName, OriginPath);

        variables = new();
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
    public void CommitToGitRunsInlineScriptWithPackageDependencyToCreateFileWhichIsCommitedToRepository()
    {
        const string packageReferenceName = "my-scripts";

        var zipPath = CreateZipWithEntry(packageReferenceName, "helper.sh", "touch \"$(get_octopusvariable 'Octopus.Calamari.Git.RepositoryPath')/proof.txt\"");

        variables.AddRange([
            new CalamariExecutionVariable(ScriptVariables.ScriptBody, $". {packageReferenceName}/helper.sh", false),
            new CalamariExecutionVariable(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString(), false),
            new CalamariExecutionVariable(PackageVariables.IndexedPackageId(packageReferenceName), packageReferenceName, false),
            new CalamariExecutionVariable(PackageVariables.IndexedOriginalPath(packageReferenceName), zipPath, false),
        ]);

        RunCommitToGit().Should().Be(0);
        GetCommittedFileContent("proof.txt").Should().NotBeNull("the helper script should have created and committed the file to the repository");
    }

    [Test]
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
    public void CanCopyPackageFilesIntoGitRepository()
    {
        const string packageReferenceName = "my-configs";
        const string destinationPath = "output-dir";

        var zipPath = CreateZipWithEntry(packageReferenceName, "configs/settings.json", "{\"setting\": \"value\"}");
        AddInputPackageVariables(packageReferenceName, zipPath, destinationPath);

        RunCommitToGit().Should().Be(0);

        GetCommittedFileContent($"{destinationPath}/configs/settings.json")
            .Should().NotBeNull("the package files should have been copied into the repository under the destination path");
    }

    [Test]
    public void SubstitutesNonSensitiveVariablesIntoAnExpandedPackageThenCopiesToGitRepository()
    {
        const string packageReferenceName = "my-configs";
        const string destinationPath = "output-dir";

        var zipPath = CreateZipWithEntry(packageReferenceName, "configs/settings.json", "{\"setting\": \"#{MyVar}\"}");
        AddInputPackageVariables(packageReferenceName, zipPath, destinationPath);

        variables.AddRange([
            // Non-sensitive variable to be substituted into the package file
            new CalamariExecutionVariable("MyVar", "production-value", false),
        ]);

        RunCommitToGit().Should().Be(0);

        var content = GetCommittedFileContent($"{destinationPath}/configs/settings.json");
        content.Should().NotBeNull("the package file should have been copied into the repository");
        content.Should().Contain("production-value", "the non-sensitive variable should have been substituted");
        content.Should().NotContain("#{MyVar}", "the Octostache template should have been replaced");
    }

    [Test]
    public void OnlyCopiesFilesMatchingInputPathsFromPackageIntoGitRepository()
    {
        const string packageReferenceName = "my-configs";
        const string destinationPath = "output-dir";

        var zipPath = CreateZipWithEntries(packageReferenceName, new Dictionary<string, string>
        {
            ["configs/settings.json"] = "{\"setting\": \"value\"}",
            ["scripts/setup.sh"] = "#!/bin/bash",
        });
        AddInputPackageVariables(packageReferenceName, zipPath, destinationPath);

        RunCommitToGit().Should().Be(0);

        GetCommittedFileContent($"{destinationPath}/configs/settings.json")
            .Should().NotBeNull("files matching the InputFilePaths glob should be copied");
        GetCommittedFileContent($"{destinationPath}/scripts/setup.sh")
            .Should().BeNull("files not matching the InputFilePaths glob should not be copied");
    }

    [Test]
    public void CopiesAllGitReferenceFilesIntoGitRepository()
    {
        const string gitDependencyName = "my-git-dep";
        const string destinationPath = "output-dir";

        var zipPath = CreateZipWithEntry(gitDependencyName, "manifests/deployment.yaml", "apiVersion: apps/v1");
        AddInputGitReferenceVariables(gitDependencyName, zipPath, destinationPath);

        RunCommitToGit().Should().Be(0);

        GetCommittedFileContent($"{destinationPath}/{gitDependencyName}/manifests/deployment.yaml")
            .Should().NotBeNull("git reference files should be copied into the repository under the destination path");
    }

    [Test]
    public void FailsWhenSensitiveVariableTemplatesAreEncounteredDuringSubstitution()
    {
        const string packageReferenceName = "my-configs";
        const string destinationPath = "output-dir";

        var zipPath = CreateZipWithEntry(packageReferenceName, "configs/settings.json", "{\"setting\": \"#{MySecret}\"}");
        AddInputPackageVariables(packageReferenceName, zipPath, destinationPath);

        variables.AddRange([
            // Sensitive variable — must NOT be substituted into repository files
            new CalamariExecutionVariable("MySecret", "super-secret-value", true),
        ]);

        RunCommitToGit().Should().NotBe(0);

        var content = GetCommittedFileContent($"{destinationPath}/configs/settings.json");
        content.Should().BeNull("the package file should not be committed when sensitive variable substitution throws");
    }

    [Test]
    public void CommitMessageSummaryIsUsedAsCommitMessage()
    {
        const string packageReferenceName = "my-configs";
        const string destinationPath = "output-dir";

        var zipPath = CreateZipWithEntry(packageReferenceName, "configs/settings.json", "{}");
        AddInputPackageVariables(packageReferenceName, zipPath, destinationPath);

        RunCommitToGit().Should().Be(0);

        using var repo = new Repository(OriginPath);
        repo.Branches[targetBranchFriendlyName].Tip.Message.Should().StartWith("Git Commit Summary");
    }

    [Test]
    public void FailingScriptResultsInNonZeroExitCode()
    {
        variables.AddRange([
            new CalamariExecutionVariable(ScriptVariables.ScriptBody, "exit 1", false),
            new CalamariExecutionVariable(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString(), false),
        ]);

        RunCommitToGit().Should().NotBe(0);
    }

    [Test]
    public void BothPackageCopyAndScriptTransformProduceFilesInRepository()
    {
        const string packageReferenceName = "my-configs";
        const string destinationPath = "output-dir";

        var zipPath = CreateZipWithEntry(packageReferenceName, "configs/settings.json", "{}");
        AddInputPackageVariables(packageReferenceName, zipPath, destinationPath);

        variables.AddRange([
            new CalamariExecutionVariable(ScriptVariables.ScriptBody, "touch \"$(get_octopusvariable 'Octopus.Calamari.Git.RepositoryPath')/script-output.txt\"", false),
            new CalamariExecutionVariable(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString(), false),
        ]);

        RunCommitToGit().Should().Be(0);
        GetCommittedFileContent($"{destinationPath}/configs/settings.json").Should().NotBeNull("package files should be copied to the repository");
        GetCommittedFileContent("script-output.txt").Should().NotBeNull("the script should have created and committed the file");
    }

    [Test]
    public void MultiplePackagesAreAllCopiedToGitRepository()
    {
        const string package1Name = "configs-package";
        const string package2Name = "templates-package";
        const string destinationPath = "output-dir";

        var zip1Path = CreateZipWithEntry(package1Name, "configs/settings.json", "{}");
        var zip2Path = CreateZipWithEntry(package2Name, "configs/template.yaml", "apiVersion: apps/v1");

        var templateValueSources =
            $"[" +
            $"{{\"Type\":\"Package\",\"PackageId\":\"{package1Name}\",\"PackageName\":\"{package1Name}\",\"InputFilePaths\":\"configs/**/*\",\"DestinationSubFolder\":\"\"}}," +
            $"{{\"Type\":\"Package\",\"PackageId\":\"{package2Name}\",\"PackageName\":\"{package2Name}\",\"InputFilePaths\":\"configs/**/*\",\"DestinationSubFolder\":\"\"}}" +
            $"]";

        variables.AddRange([
            new CalamariExecutionVariable(PackageVariables.IndexedPackageId(package1Name), package1Name, false),
            new CalamariExecutionVariable(PackageVariables.IndexedOriginalPath(package1Name), zip1Path, false),
            new CalamariExecutionVariable(PackageVariables.IndexedPackageId(package2Name), package2Name, false),
            new CalamariExecutionVariable(PackageVariables.IndexedOriginalPath(package2Name), zip2Path, false),
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.TemplateFileSources, templateValueSources, false),
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.DestinationPath, destinationPath, false),
        ]);

        RunCommitToGit().Should().Be(0);
        GetCommittedFileContent($"{destinationPath}/configs/settings.json").Should().NotBeNull("files from the first package should be copied");
        GetCommittedFileContent($"{destinationPath}/configs/template.yaml").Should().NotBeNull("files from the second package should be copied");
    }

    [Test]
    public void CommitToGitRunsScriptProvidedViaScriptBodyBySyntaxVariable()
    {
        variables.AddRange([
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.Bash), "touch \"$(get_octopusvariable 'Octopus.Calamari.Git.RepositoryPath')/proof.txt\"", false),
        ]);

        RunCommitToGit().Should().Be(0);
        GetCommittedFileContent("proof.txt").Should().NotBeNull("script provided via syntax-specific variable should run and commit the file");
    }

    [Test]
    public void WhenNoScriptAndNoPackagesCommandSucceedsWithZeroExitCode()
    {
        RunCommitToGit().Should().Be(0);
    }

    [Test]
    public void WhenBothScriptParametersArgAndVariableAreSetVariableTakesPrecedence()
    {
        var proofFile = Path.Combine(executionDirectory, "arg_check.txt");
        var zipPath = CreateZipWithEntry("transform-script", "script.sh",
            $"if [ \"$1\" = \"from-variable\" ]; then touch {proofFile}; fi");

        variables.AddRange([
            new CalamariExecutionVariable(ScriptVariables.ScriptFileName, "script.sh", false),
            new CalamariExecutionVariable(ScriptVariables.ScriptParameters, "from-variable", false),
        ]);

        RunCommitToGit("--package", zipPath, "--scriptParameters", "from-arg").Should().Be(0);
        File.Exists(proofFile).Should().BeTrue("the variable value should take precedence over the --scriptParameters arg");
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

    string CreateZipWithEntries(string packageName, Dictionary<string, string> entries)
    {
        var zipPath = Path.Combine(executionDirectory, $"{packageName}.1.0.0.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var (entryPath, content) in entries)
            {
                var entry = archive.CreateEntry(entryPath);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }
        return zipPath;
    }

    void AddInputPackageVariables(string packageReferenceName, string zipPath, string destinationPath)
    {
        var templateValueSources = $"[{{\"Type\":\"Package\",\"PackageId\":\"{packageReferenceName}\",\"PackageName\":\"{packageReferenceName}\",\"InputFilePaths\":\"configs/**/*\",\"DestinationSubFolder\":\"\"}}]";
        variables.AddRange([
            // Package to copy into the repository, declared via TemplateValuesSources
            new CalamariExecutionVariable(PackageVariables.IndexedPackageId(packageReferenceName), packageReferenceName, false),
            new CalamariExecutionVariable(PackageVariables.IndexedOriginalPath(packageReferenceName), zipPath, false),
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.TemplateFileSources, templateValueSources, false),
            // Override the destination path set in setUp
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.DestinationPath, destinationPath, false),
        ]);
    }

    void AddInputGitReferenceVariables(string gitDependencyName, string zipPath, string destinationPath)
    {
        var templateValueSources = $"[{{\"Type\":\"GitRepository\",\"GitDependencyName\":\"{gitDependencyName}\",\"DestinationSubFolder\":\"{destinationPath}\"}}]";
        variables.AddRange([
            new CalamariExecutionVariable(Deployment.SpecialVariables.GitResources.Extract(gitDependencyName), "true", false),
            new CalamariExecutionVariable(Deployment.SpecialVariables.GitResources.OriginalPath(gitDependencyName), zipPath, false),
            new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.TemplateFileSources, templateValueSources, false),
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
