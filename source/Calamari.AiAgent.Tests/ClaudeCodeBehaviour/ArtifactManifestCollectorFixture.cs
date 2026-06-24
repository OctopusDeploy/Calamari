using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common.Commands;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class ArtifactManifestCollectorFixture
{
    string workingDir = null!;
    string destinationRoot = null!;

    [SetUp]
    public void SetUp()
    {
        workingDir = Path.Combine(Path.GetTempPath(), $"test-artifacts-wd-{Path.GetRandomFileName()}");
        destinationRoot = Path.Combine(Path.GetTempPath(), $"test-artifacts-dest-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);
        Directory.CreateDirectory(destinationRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(workingDir)) Directory.Delete(workingDir, true);
        if (Directory.Exists(destinationRoot)) Directory.Delete(destinationRoot, true);
    }

    void WriteManifest(params string[] lines)
    {
        var dir = Path.Combine(workingDir, ".octopus");
        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, "artifacts.jsonl"), lines);
    }

    string WriteWorkingFile(string relativePath, string content = "data")
    {
        var full = Path.Combine(workingDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    IReadOnlyList<CapturedArtifact> Collect() => new ArtifactManifestCollector().Collect(workingDir, destinationRoot);

    [Test]
    public void NoManifest_ReturnsEmpty()
    {
        Collect().Should().BeEmpty();
    }

    [Test]
    public void EmptyManifest_ReturnsEmpty()
    {
        WriteManifest();
        Collect().Should().BeEmpty();
    }

    [Test]
    public void BlankLines_AreIgnored()
    {
        WriteWorkingFile("report.csv");
        WriteManifest("", "   ", """{"path":"report.csv"}""", "");

        Collect().Should().HaveCount(1);
    }

    [Test]
    public void SingleFile_IsCopiedIntoArtifactsDir_AndReturned()
    {
        WriteWorkingFile("report.csv", "hello");
        WriteManifest("""{"path":"report.csv","name":"My Report"}""");

        var captured = Collect();

        captured.Should().HaveCount(1);
        captured[0].Name.Should().Be("My Report");
        captured[0].Length.Should().Be(5);
        captured[0].Path.Should().Be(Path.Combine(destinationRoot, "artifacts", "report.csv"));
        File.Exists(captured[0].Path).Should().BeTrue();
        File.ReadAllText(captured[0].Path).Should().Be("hello");
    }

    [Test]
    public void File_LeftIntactInWorkingDir()
    {
        var source = WriteWorkingFile("report.csv");
        WriteManifest("""{"path":"report.csv"}""");

        Collect();

        File.Exists(source).Should().BeTrue();
    }

    [Test]
    public void Name_DefaultsToFileName_WhenOmitted()
    {
        WriteWorkingFile("output/data.csv");
        WriteManifest("""{"path":"output/data.csv"}""");

        Collect().Single().Name.Should().Be("data.csv");
    }

    [Test]
    public void MultipleFiles_AreEachCaptured()
    {
        WriteWorkingFile("a.txt");
        WriteWorkingFile("b.txt");
        WriteManifest("""{"path":"a.txt"}""", """{"path":"b.txt"}""");

        Collect().Select(c => Path.GetFileName(c.Path)).Should().BeEquivalentTo("a.txt", "b.txt");
    }

    [Test]
    public void TwoFilesSharingBaseName_AreKeptDistinct_ByRelativePath()
    {
        WriteWorkingFile("one/data.csv", "1");
        WriteWorkingFile("two/data.csv", "22");
        WriteManifest("""{"path":"one/data.csv"}""", """{"path":"two/data.csv"}""");

        var captured = Collect();

        captured.Should().HaveCount(2);
        captured.Select(c => c.Path).Should().OnlyHaveUniqueItems();
        captured.Should().Contain(c => c.Path.EndsWith(Path.Combine("artifacts", "one", "data.csv")));
        captured.Should().Contain(c => c.Path.EndsWith(Path.Combine("artifacts", "two", "data.csv")));
    }

    [Test]
    public void MissingFile_Throws()
    {
        WriteManifest("""{"path":"nope.csv"}""");

        var act = () => Collect();

        act.Should().Throw<CommandException>().WithMessage("*does not exist*");
    }

    [Test]
    public void MalformedJsonLine_Throws()
    {
        WriteManifest("not json");

        var act = () => Collect();

        act.Should().Throw<CommandException>().WithMessage("*not valid JSON*");
    }

    [Test]
    public void EmptyPath_Throws()
    {
        WriteManifest("""{"path":""}""");

        var act = () => Collect();

        act.Should().Throw<CommandException>().WithMessage("*path*");
    }

    [Test]
    public void AbsolutePathOutsideWorkingDir_Throws()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"outside-{Path.GetRandomFileName()}.txt");
        File.WriteAllText(outside, "secret");
        try
        {
            WriteManifest($$"""{"path":{{System.Text.Json.JsonSerializer.Serialize(outside)}}}""");

            var act = () => Collect();

            act.Should().Throw<CommandException>().WithMessage("*outside the working directory*");
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Test]
    [Platform(Exclude = "Win", Reason = "Symlink creation requires elevation on Windows.")]
    public void SymlinkEscapingWorkingDir_Throws()
    {
        var secret = Path.Combine(Path.GetTempPath(), $"secret-{Path.GetRandomFileName()}.txt");
        File.WriteAllText(secret, "secret");
        try
        {
            File.CreateSymbolicLink(Path.Combine(workingDir, "link.txt"), secret);
            WriteManifest("""{"path":"link.txt"}""");

            var act = () => Collect();

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
        WriteWorkingFile("site/index.html", "<h1>hi</h1>");
        WriteWorkingFile("site/css/app.css", "body{}");
        WriteManifest("""{"path":"site","name":"Generated Website"}""");

        var captured = Collect();

        captured.Should().HaveCount(1);
        captured[0].Name.Should().Be("Generated Website");
        captured[0].Path.Should().Be(Path.Combine(destinationRoot, "artifacts", "site.zip"));
        File.Exists(captured[0].Path).Should().BeTrue();

        using var archive = ZipFile.OpenRead(captured[0].Path);
        archive.Entries.Select(e => e.FullName.Replace('\\', '/'))
               .Should().BeEquivalentTo("index.html", "css/app.css");
    }

    [Test]
    public void Directory_DefaultsNameToDirNameWithZipExtension()
    {
        WriteWorkingFile("site/index.html");
        WriteManifest("""{"path":"site"}""");

        Collect().Single().Name.Should().Be("site.zip");
    }

    [Test]
    public void EmptyDirectory_Throws()
    {
        Directory.CreateDirectory(Path.Combine(workingDir, "empty"));
        WriteManifest("""{"path":"empty"}""");

        var act = () => Collect();

        act.Should().Throw<CommandException>().WithMessage("*empty*");
    }

    [Test]
    public void WorkingDirRoot_Throws()
    {
        WriteWorkingFile("report.csv");
        WriteManifest("""{"path":"."}""");

        var act = () => Collect();

        act.Should().Throw<CommandException>().WithMessage("*working directory itself*");
    }
}
