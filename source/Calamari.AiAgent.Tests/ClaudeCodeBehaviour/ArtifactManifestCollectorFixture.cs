using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Requirements;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class ArtifactManifestCollectorFixture
{
    [Test]
    public void NoManifest_ReturnsEmpty()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();

        Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath).Should().BeEmpty();
    }

    [Test]
    public void EmptyManifest_ReturnsEmpty()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        WriteManifest(workingDir.DirectoryPath);

        Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath).Should().BeEmpty();
    }

    [Test]
    public void BlankLines_AreIgnored()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        WriteWorkingFile(workingDir.DirectoryPath, "report.csv");
        WriteManifest(workingDir.DirectoryPath, "", "   ", """{"path":"report.csv"}""", "");

        Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath).Should().HaveCount(1);
    }

    [Test]
    public void SingleFile_IsCopiedIntoArtifactsDir_AndReturned()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        WriteWorkingFile(workingDir.DirectoryPath, "report.csv", "hello");
        WriteManifest(workingDir.DirectoryPath, """{"path":"report.csv","name":"My Report"}""");

        var captured = Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath);

        captured.Should().BeEquivalentTo([
            new StagedArtifact(Path.Combine(destinationRoot.DirectoryPath, "artifacts", "report.csv"), "My Report", 5 )
        ]);
        File.Exists(captured[0].Path).Should().BeTrue();
        File.ReadAllText(captured[0].Path).Should().Be("hello");
    }

    [Test]
    public void File_LeftIntactInWorkingDir()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        var source = WriteWorkingFile(workingDir.DirectoryPath, "report.csv");
        WriteManifest(workingDir.DirectoryPath, """{"path":"report.csv"}""");

        Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath);

        File.Exists(source).Should().BeTrue();
    }

    [Test]
    public void Name_DefaultsToFileName_WhenOmitted()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        WriteWorkingFile(workingDir.DirectoryPath, "output/data.csv");
        WriteManifest(workingDir.DirectoryPath, """{"path":"output/data.csv"}""");

        Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath).Single().Name.Should().Be("data.csv");
    }

    [Test]
    public void MultipleFiles_AreEachCaptured()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        WriteWorkingFile(workingDir.DirectoryPath, "a.txt");
        WriteWorkingFile(workingDir.DirectoryPath, "b.txt");
        WriteManifest(workingDir.DirectoryPath, """{"path":"a.txt"}""", """{"path":"b.txt"}""");

        var captured = Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath);
            
        captured.Select(c => Path.GetFileName(c.Path)).Should().BeEquivalentTo("a.txt", "b.txt");
    }

    [Test]
    public void TwoFilesSharingBaseName_AreKeptDistinct_ByRelativePath()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        WriteWorkingFile(workingDir.DirectoryPath, "one/data.csv", "1");
        WriteWorkingFile(workingDir.DirectoryPath, "two/data.csv", "22");
        WriteManifest(workingDir.DirectoryPath, """{"path":"one/data.csv"}""", """{"path":"two/data.csv"}""");

        var captured = Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath);

        captured.Should().HaveCount(2);
        captured.Should().Contain(c => c.Path.EndsWith(Path.Combine("artifacts", "one", "data.csv")));
        captured.Should().Contain(c => c.Path.EndsWith(Path.Combine("artifacts", "two", "data.csv")));
    }

    [Test]
    public void MissingFile_Throws()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        WriteManifest(workingDir.DirectoryPath, """{"path":"nope.csv"}""");

        var act = () => Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath);

        act.Should().Throw<CommandException>().WithMessage("*does not exist*");
    }

    [Test]
    public void MalformedJsonLine_Throws()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        WriteManifest(workingDir.DirectoryPath, "not json");

        var act = () => Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath);

        act.Should().Throw<CommandException>().WithMessage("*not valid JSON*");
    }

    [Test]
    public void EmptyPath_Throws()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        WriteManifest(workingDir.DirectoryPath, """{"path":""}""");

        var act = () => Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath);

        act.Should().Throw<CommandException>().WithMessage("*path*");
    }

    [Test]
    public void AbsolutePathOutsideWorkingDir_Throws()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        var outside = Path.Combine(Path.GetTempPath(), $"outside-{Path.GetRandomFileName()}.txt");
        File.WriteAllText(outside, "secret");
        try
        {
            WriteManifest(workingDir.DirectoryPath, $$"""{"path":{{System.Text.Json.JsonSerializer.Serialize(outside)}}}""");

            var act = () => Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath);

            act.Should().Throw<CommandException>().WithMessage("*outside the working directory*");
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Test]
    [NonWindowsTest]
    public void SymlinkEscapingWorkingDir_Throws()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        var secret = Path.Combine(Path.GetTempPath(), $"secret-{Path.GetRandomFileName()}.txt");
        File.WriteAllText(secret, "secret");
        try
        {
            File.CreateSymbolicLink(Path.Combine(workingDir.DirectoryPath, "link.txt"), secret);
            WriteManifest(workingDir.DirectoryPath, """{"path":"link.txt"}""");

            var act = () => Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath);

            act.Should().Throw<CommandException>().WithMessage("*outside the working directory*");
        }
        finally
        {
            File.Delete(secret);
        }
    }

    [Test]
    public void Directory_IsZippedIntoSingleArtifact()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        WriteWorkingFile(workingDir.DirectoryPath, "site/index.html", "<h1>hi</h1>");
        WriteWorkingFile(workingDir.DirectoryPath, "site/css/app.css", "body{}");
        WriteManifest(workingDir.DirectoryPath, """{"path":"site","name":"Generated Website"}""");

        var captured = Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath);

        captured.Should().HaveCount(1);
        captured[0].Name.Should().Be("Generated Website");
        captured[0].Path.Should().Be(Path.Combine(destinationRoot.DirectoryPath, "artifacts", "site.zip"));
        File.Exists(captured[0].Path).Should().BeTrue();

        using var archive = ZipFile.OpenRead(captured[0].Path);
        archive.Entries.Select(e => e.FullName.Replace('\\', '/'))
               .Should().BeEquivalentTo("index.html", "css/app.css");
    }

    [Test]
    public void Directory_DefaultsNameToDirNameWithZipExtension()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        WriteWorkingFile(workingDir.DirectoryPath, "site/index.html");
        WriteManifest(workingDir.DirectoryPath, """{"path":"site"}""");

        Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath).Single().Name.Should().Be("site.zip");
    }

    [Test]
    public void EmptyDirectory_Throws()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        Directory.CreateDirectory(Path.Combine(workingDir.DirectoryPath, "empty"));
        WriteManifest(workingDir.DirectoryPath, """{"path":"empty"}""");

        var act = () => Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath);

        act.Should().Throw<CommandException>().WithMessage("*is empty*");
    }

    [Test]
    public void WorkingDirRoot_Throws()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        WriteWorkingFile(workingDir.DirectoryPath, "report.csv");
        WriteManifest(workingDir.DirectoryPath, """{"path":"."}""");

        var act = () => Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath);

        act.Should().Throw<CommandException>().WithMessage("*working directory itself*");
    }

    [Test]
    public void TotalSizeWithinLimit_DoesNotThrow()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        WriteWorkingFile(workingDir.DirectoryPath, "small.txt", "tiny");
        WriteManifest(workingDir.DirectoryPath, """{"path":"small.txt"}""");

        var captured = Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath, VariablesWithMaxArtifactMegaBytes(1));

        captured.Should().HaveCount(1);
    }

    [Test]
    public void TotalSizeExceedingLimit_Throws()
    {
        using var workingDir = TemporaryDirectory.Create();
        using var destinationRoot = TemporaryDirectory.Create();
        WriteWorkingFile(workingDir.DirectoryPath, "big.txt", new string('x', 2 * 1024 * 1024));
        WriteManifest(workingDir.DirectoryPath, """{"path":"big.txt"}""");

        var act = () => Collect(workingDir.DirectoryPath, destinationRoot.DirectoryPath, VariablesWithMaxArtifactMegaBytes(1));

        act.Should().Throw<CommandException>().WithMessage("*maximum total*");
    }
    
    static void WriteManifest(string workingDir, params string[] lines)
    {
        var dir = Path.Combine(workingDir, ".octopus");
        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, "artifacts.jsonl"), lines);
    }

    static string WriteWorkingFile(string workingDir, string relativePath, string content = "data")
    {
        var full = Path.Combine(workingDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    static IReadOnlyList<StagedArtifact> Collect(string workingDir, string destinationRoot)
        => Collect(workingDir, destinationRoot, new CalamariVariables());

    static IReadOnlyList<StagedArtifact> Collect(string workingDir, string destinationRoot, IVariables variables)
        => new ArtifactManifestCollector(variables).Collect(workingDir, destinationRoot);

    static CalamariVariables VariablesWithMaxArtifactMegaBytes(int megabytes)
    {
        var variables = new CalamariVariables();
        variables.Set(SpecialVariables.Action.Claude.MaxArtifactSizeInMegaBytes, megabytes.ToString());
        return variables;
    }
}
