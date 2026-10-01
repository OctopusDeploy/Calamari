using System;
using System.IO;
using System.IO.Compression;
using Calamari.Common.Features.Scripts;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NUnit.Framework;
using SpecialVariables = Calamari.Deployment.SpecialVariables;

namespace Calamari.Tests.Fixtures.Commands;

[TestFixture]
[Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
public class RunScriptGitDependenciesFixture
{
    const string GitDependencyName = "my-git-dep";
    const string VariablePassword = "password";

    readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();

    string executionDirectory;
    string originalCwd;
    CalamariExecutionVariableCollection variables;

    string ExtractedDependencyDirectory => Path.Combine(executionDirectory, GitDependencyName);

    [SetUp]
    public void SetUp()
    {
        executionDirectory = fileSystem.CreateTemporaryDirectory();
        variables = new CalamariExecutionVariableCollection();

        originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(executionDirectory);
    }

    [TearDown]
    public void Cleanup()
    {
        Directory.SetCurrentDirectory(originalCwd);
        fileSystem.DeleteDirectory(executionDirectory);
    }

    [Test]
    public void GitDependencyIsExtractedAndUsableByScriptWhenFeatureToggleIsEnabled()
    {
        var proofFile = Path.Combine(executionDirectory, "proof.txt");
        var zipPath = CreateZipWithEntry("scripts/helper.sh", $"touch \"{proofFile}\"");

        AddGitDependencyVariables(zipPath);
        EnableGitDependenciesFeatureToggle();
        AddScript($". {GitDependencyName}/scripts/helper.sh");

        RunScript().Should().Be(0);

        File.Exists(Path.Combine(ExtractedDependencyDirectory, "scripts", "helper.sh"))
            .Should().BeTrue("the git dependency should be extracted into a folder named after the dependency in the working directory");
        File.Exists(proofFile).Should().BeTrue("the script should have been able to source the helper from the extracted git dependency");
    }

    [Test]
    public void GitDependencyIsExtractedEvenWhenExtractFlagIsFalse()
    {
        var zipPath = CreateZipWithEntry("scripts/helper.sh", "echo hello");

        AddGitDependencyVariables(zipPath);
        variables.Add(new CalamariExecutionVariable(SpecialVariables.GitResources.Extract(GitDependencyName), "False", false));
        EnableGitDependenciesFeatureToggle();
        AddScript("echo running");

        RunScript().Should().Be(0);

        File.Exists(Path.Combine(ExtractedDependencyDirectory, "scripts", "helper.sh"))
            .Should().BeTrue("git dependencies should always be extracted, even when the Extract variable is false");
    }

    [Test]
    public void GitDependencyIsNotStagedWhenFeatureToggleIsNotEnabled()
    {
        var zipPath = CreateZipWithEntry("scripts/helper.sh", "echo hello");

        AddGitDependencyVariables(zipPath);
        AddScript("echo running");

        RunScript().Should().Be(0);

        Directory.Exists(ExtractedDependencyDirectory)
            .Should().BeFalse("git dependencies should not be staged when the feature toggle is not enabled");
    }

    int RunScript()
    {
        var variablesPath = Path.Combine(executionDirectory, "variables.json");
        File.WriteAllBytes(variablesPath, AesEncryption.ForServerVariables(VariablePassword).Encrypt(variables.ToJsonString()));

        return Program.Main(new[]
        {
            "run-script",
            "--variables", variablesPath,
            "--variablesPassword", VariablePassword,
        });
    }

    void AddScript(string scriptBody)
    {
        variables.AddRange(new[]
        {
            new CalamariExecutionVariable(ScriptVariables.ScriptBody, scriptBody, false),
            new CalamariExecutionVariable(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString(), false),
        });
    }

    void AddGitDependencyVariables(string zipPath)
    {
        variables.Add(new CalamariExecutionVariable(SpecialVariables.GitResources.OriginalPath(GitDependencyName), zipPath, false));
    }

    void EnableGitDependenciesFeatureToggle()
    {
        variables.Add(new CalamariExecutionVariable(KnownVariables.EnabledFeatureToggles, OctopusFeatureToggles.KnownSlugs.GitDependenciesForScriptsFeatureToggle, false));
    }

    string CreateZipWithEntry(string entryPath, string content)
    {
        var zipPath = Path.Combine(executionDirectory, $"{GitDependencyName}.1.0.0.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry(entryPath);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        return zipPath;
    }
}
