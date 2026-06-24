# AiAgent Artifact Manifest Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the Claude Code AiAgent step capture files and directories it creates as Octopus artifacts, declared explicitly by the agent via a manifest file and captured deterministically by Calamari.

**Architecture:** The agent writes declarations to `workingDir/.octopus/artifacts.jsonl` (gated by an always-on `octopus-artifacts` skill). After the CLI run returns — and before the temporary working directory is disposed — a new `ArtifactManifestCollector` reads the manifest, validates each entry (hard-fail on anything invalid), copies files (or zips directories) out of the temp dir into `calamariDir/artifacts/`, and returns the captured artifacts. `InvokeClaudeCodeBehaviour` then emits one `NewOctopusArtifact` service message per captured artifact.

**Tech Stack:** C# / .NET 8, NUnit + FluentAssertions, BCL only (`System.IO.Compression.ZipFile` for bundling). No new package dependencies.

## Global Constraints

- Target framework: **net8.0**. `TreatWarningsAsErrors` is **true** in the test project — no unused usings, no nullable warnings.
- Test framework: **NUnit 3** with **FluentAssertions 7**. Follow existing fixtures in `Calamari.AiAgent.Tests/ClaudeCodeBehaviour/` (temp `workingDir` in `[SetUp]`, cleanup in `[TearDown]`).
- **No new package dependencies** and **no new SpecialVariables**.
- Security boundary: an artifact entry may only reference a path whose **canonical real path** is strictly inside the working directory. Reject the working-dir root itself.
- Error handling: **hard-fail** — any invalid manifest entry throws `Calamari.Common.Commands.CommandException`, failing the step.
- Manifest location: `workingDir/.octopus/artifacts.jsonl`, one JSON object (`{"path": "...", "name": "..."}`) per line; `name` optional.
- Capture destination: `calamariDir/artifacts/<relativePathFromWorkingDir>` (files) or `…<relativePathFromWorkingDir>.zip` (directories), where `calamariDir = context.CurrentDirectory`.
- The skill `.md` is auto-embedded by the existing csproj glob `ClaudeCodeBehaviour\DefaultContext\Skills\**\*.md` — no csproj change needed.

---

### Task 1: `ArtifactManifestCollector` — file capture + validation

**Files:**
- Create: `Calamari.AiAgent/ClaudeCodeBehaviour/ArtifactManifestCollector.cs`
- Test: `Calamari.AiAgent.Tests/ClaudeCodeBehaviour/ArtifactManifestCollectorFixture.cs`

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces:
  - `public record CapturedArtifact(string Path, string Name, long Length);`
  - `public class ArtifactManifestCollector` with
    `public IReadOnlyList<CapturedArtifact> Collect(string workingDir, string destinationRoot)`.
  - `Collect` returns an empty list when the manifest is absent/empty; throws `CommandException` on any invalid entry; for a valid **file** entry copies it to `destinationRoot/artifacts/<relpath>` and returns a `CapturedArtifact` (directory entries are added in Task 2).

- [ ] **Step 1: Write the failing tests**

Create `Calamari.AiAgent.Tests/ClaudeCodeBehaviour/ArtifactManifestCollectorFixture.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
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
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test source/Calamari.AiAgent.Tests/Calamari.AiAgent.Tests.csproj --filter "FullyQualifiedName~ArtifactManifestCollectorFixture"`
Expected: FAIL to compile — `ArtifactManifestCollector` / `CapturedArtifact` do not exist.

- [ ] **Step 3: Write the implementation**

Create `Calamari.AiAgent/ClaudeCodeBehaviour/ArtifactManifestCollector.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Calamari.Common.Commands;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public record CapturedArtifact(string Path, string Name, long Length);

public class ArtifactManifestCollector
{
    const string ArtifactsDirName = "artifacts";

    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<CapturedArtifact> Collect(string workingDir, string destinationRoot)
    {
        var manifestPath = Path.Combine(workingDir, ".octopus", "artifacts.jsonl");
        if (!File.Exists(manifestPath))
            return Array.Empty<CapturedArtifact>();

        var canonicalWorkingDir = Canonical(workingDir);
        var artifactsDir = Path.Combine(destinationRoot, ArtifactsDirName);

        var captured = new List<CapturedArtifact>();
        var lineNumber = 0;
        foreach (var rawLine in File.ReadAllLines(manifestPath))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var entry = ParseEntry(line, lineNumber);
            captured.Add(Capture(entry, lineNumber, workingDir, canonicalWorkingDir, artifactsDir));
        }

        return captured;
    }

    static ManifestEntry ParseEntry(string line, int lineNumber)
    {
        ManifestEntry? entry;
        try
        {
            entry = JsonSerializer.Deserialize<ManifestEntry>(line, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new CommandException($"Artifact manifest line {lineNumber} is not valid JSON: {ex.Message}");
        }

        if (entry is null || string.IsNullOrWhiteSpace(entry.Path))
            throw new CommandException($"Artifact manifest line {lineNumber} is missing a 'path'.");

        return entry;
    }

    static CapturedArtifact Capture(ManifestEntry entry, int lineNumber, string workingDir, string canonicalWorkingDir, string artifactsDir)
    {
        var full = Path.GetFullPath(Path.IsPathRooted(entry.Path!) ? entry.Path! : Path.Combine(workingDir, entry.Path!));

        if (!File.Exists(full))
            throw new CommandException($"Artifact manifest line {lineNumber}: '{entry.Path}' does not exist.");

        EnsureInsideWorkingDir(entry, lineNumber, full, canonicalWorkingDir);

        var relative = Path.GetRelativePath(workingDir, full);
        var destPath = Path.Combine(artifactsDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.Copy(full, destPath, overwrite: true);

        var name = entry.Name ?? Path.GetFileName(full);
        return new CapturedArtifact(destPath, name, new FileInfo(destPath).Length);
    }

    static void EnsureInsideWorkingDir(ManifestEntry entry, int lineNumber, string full, string canonicalWorkingDir)
    {
        var canonical = Canonical(full);
        if (!canonical.StartsWith(canonicalWorkingDir + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new CommandException($"Artifact manifest line {lineNumber}: '{entry.Path}' resolves outside the working directory.");
    }

    // Resolves a symlinked leaf to its real target so a link inside the working dir
    // cannot point at a file outside it. (Symlinked intermediate directories are a
    // known v1 limitation — see the design doc follow-ups.)
    static string Canonical(string path)
    {
        var full = Path.GetFullPath(path);
        FileSystemInfo info = Directory.Exists(full) ? new DirectoryInfo(full) : new FileInfo(full);
        var target = info.ResolveLinkTarget(returnFinalTarget: true);
        return target?.FullName ?? full;
    }

    class ManifestEntry
    {
        public string? Path { get; set; }
        public string? Name { get; set; }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test source/Calamari.AiAgent.Tests/Calamari.AiAgent.Tests.csproj --filter "FullyQualifiedName~ArtifactManifestCollectorFixture"`
Expected: PASS (all Task 1 tests; the symlink test is skipped on Windows).

- [ ] **Step 5: Commit**

```bash
git add source/Calamari.AiAgent/ClaudeCodeBehaviour/ArtifactManifestCollector.cs source/Calamari.AiAgent.Tests/ClaudeCodeBehaviour/ArtifactManifestCollectorFixture.cs
git commit -m "Add ArtifactManifestCollector with file capture and validation"
```

---

### Task 2: Directory entries → single zip bundle

**Files:**
- Modify: `Calamari.AiAgent/ClaudeCodeBehaviour/ArtifactManifestCollector.cs`
- Test: `Calamari.AiAgent.Tests/ClaudeCodeBehaviour/ArtifactManifestCollectorFixture.cs`

**Interfaces:**
- Consumes: `ArtifactManifestCollector.Collect` and `CapturedArtifact` from Task 1.
- Produces: `Collect` now also accepts a directory `path`; it zips the directory into `destinationRoot/artifacts/<relpath>.zip` and returns one `CapturedArtifact` for it. Empty directories and an entry resolving to the working-dir root throw `CommandException`.

- [ ] **Step 1: Write the failing tests**

Add these methods to `ArtifactManifestCollectorFixture` (add `using System.IO.Compression;` to the file's usings):

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test source/Calamari.AiAgent.Tests/Calamari.AiAgent.Tests.csproj --filter "FullyQualifiedName~ArtifactManifestCollectorFixture"`
Expected: FAIL — `Directory_IsZippedIntoSingleArtifact` etc. throw "does not exist" (directories aren't handled yet) and `WorkingDirRoot_Throws` doesn't throw the expected message.

- [ ] **Step 3: Update the implementation**

Add `using System.IO.Compression;` to the top of `ArtifactManifestCollector.cs`. Replace the `Capture` method with:

```csharp
    static CapturedArtifact Capture(ManifestEntry entry, int lineNumber, string workingDir, string canonicalWorkingDir, string artifactsDir)
    {
        var full = Path.GetFullPath(Path.IsPathRooted(entry.Path!) ? entry.Path! : Path.Combine(workingDir, entry.Path!));

        var isDirectory = Directory.Exists(full);
        if (!isDirectory && !File.Exists(full))
            throw new CommandException($"Artifact manifest line {lineNumber}: '{entry.Path}' does not exist.");

        var canonical = Canonical(full);
        if (string.Equals(canonical, canonicalWorkingDir, StringComparison.Ordinal))
            throw new CommandException($"Artifact manifest line {lineNumber}: cannot attach the working directory itself; use a file or subdirectory.");
        if (!canonical.StartsWith(canonicalWorkingDir + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new CommandException($"Artifact manifest line {lineNumber}: '{entry.Path}' resolves outside the working directory.");

        var relative = Path.GetRelativePath(workingDir, full);

        return isDirectory
            ? CaptureDirectory(entry, lineNumber, full, relative, artifactsDir)
            : CaptureFile(entry, full, relative, artifactsDir);
    }

    static CapturedArtifact CaptureFile(ManifestEntry entry, string full, string relative, string artifactsDir)
    {
        var destPath = Path.Combine(artifactsDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.Copy(full, destPath, overwrite: true);

        var name = entry.Name ?? Path.GetFileName(full);
        return new CapturedArtifact(destPath, name, new FileInfo(destPath).Length);
    }

    static CapturedArtifact CaptureDirectory(ManifestEntry entry, int lineNumber, string full, string relative, string artifactsDir)
    {
        if (Directory.GetFileSystemEntries(full).Length == 0)
            throw new CommandException($"Artifact manifest line {lineNumber}: directory '{entry.Path}' is empty.");

        var zipPath = Path.Combine(artifactsDir, relative + ".zip");
        Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
        if (File.Exists(zipPath))
            File.Delete(zipPath);
        ZipFile.CreateFromDirectory(full, zipPath);

        var dirName = new DirectoryInfo(full).Name;
        var name = entry.Name ?? dirName + ".zip";
        return new CapturedArtifact(zipPath, name, new FileInfo(zipPath).Length);
    }
```

The Task 1 `EnsureInsideWorkingDir` helper is now inlined into `Capture` (with the added root check) and can be deleted.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test source/Calamari.AiAgent.Tests/Calamari.AiAgent.Tests.csproj --filter "FullyQualifiedName~ArtifactManifestCollectorFixture"`
Expected: PASS (all Task 1 + Task 2 tests).

- [ ] **Step 5: Commit**

```bash
git add source/Calamari.AiAgent/ClaudeCodeBehaviour/ArtifactManifestCollector.cs source/Calamari.AiAgent.Tests/ClaudeCodeBehaviour/ArtifactManifestCollectorFixture.cs
git commit -m "Support directory artifact entries as zip bundles"
```

---

### Task 3: `octopus-artifacts` skill

**Files:**
- Create: `Calamari.AiAgent/ClaudeCodeBehaviour/DefaultContext/Skills/octopus-artifacts.md`
- Test: `Calamari.AiAgent.Tests/ClaudeCodeBehaviour/SkillsWriterFixture.cs`

**Interfaces:**
- Consumes: the existing `SkillsWriter.SetupSkills` (writes every embedded skill resource to `.claude/skills/<name>/SKILL.md`).
- Produces: a new system skill `octopus-artifacts` present in every agent run.

- [ ] **Step 1: Write the failing test**

Add to `SkillsWriterFixture`:

```csharp
    [Test]
    public void SetupSkills_WritesArtifactsSkill()
    {
        new SkillsWriter(EmptyVariables()).SetupSkills(workingDir);

        var skillMd = Path.Combine(workingDir, ".claude", "skills", "octopus-artifacts", "SKILL.md");
        File.Exists(skillMd).Should().BeTrue();

        var content = File.ReadAllText(skillMd);
        content.Should().Contain(".octopus/artifacts.jsonl");
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test source/Calamari.AiAgent.Tests/Calamari.AiAgent.Tests.csproj --filter "FullyQualifiedName~SkillsWriterFixture.SetupSkills_WritesArtifactsSkill"`
Expected: FAIL — the `octopus-artifacts` skill file is not written (resource does not exist).

- [ ] **Step 3: Create the skill resource**

Create `Calamari.AiAgent/ClaudeCodeBehaviour/DefaultContext/Skills/octopus-artifacts.md`:

```markdown
---
name: octopus-artifacts
description: Use ONLY when the user explicitly asks to attach, upload, or save output as an Octopus artifact. Do not infer artifacts from a request that merely creates files.
---
You can publish files you create as Octopus **artifacts** so they can be collected after this step.

Do this ONLY when the user explicitly asks to attach, upload, or save something as an artifact. Never infer it from a plain "create a file" request.

To publish artifacts:
1. Create the output **inside the current working directory** (this is your default directory). Do not write artifacts to `/tmp` or other locations.
2. Append one line per artifact to `.octopus/artifacts.jsonl` (create the `.octopus` directory and the file if they do not exist). Each line is a JSON object:
   `{"path": "<path relative to the working directory>", "name": "<optional display name>"}`

Rules:
- For several individual files, add **one line per file**.
- For many related files (for example a generated website), put them in a **dedicated subdirectory** and add a single line for that directory — it will be zipped into one artifact.
- Do **not** attach the working directory itself; always attach a specific file or subdirectory.
- `name` is optional; it defaults to the file name (or `<directory>.zip` for a directory).
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test source/Calamari.AiAgent.Tests/Calamari.AiAgent.Tests.csproj --filter "FullyQualifiedName~SkillsWriterFixture"`
Expected: PASS (the new test plus the existing `SkillsWriter` tests).

- [ ] **Step 5: Commit**

```bash
git add source/Calamari.AiAgent/ClaudeCodeBehaviour/DefaultContext/Skills/octopus-artifacts.md source/Calamari.AiAgent.Tests/ClaudeCodeBehaviour/SkillsWriterFixture.cs
git commit -m "Add octopus-artifacts skill instructing manifest usage"
```

---

### Task 4: Wire the collector into `InvokeClaudeCodeBehaviour`

**Files:**
- Modify: `Calamari.AiAgent/ClaudeCodeBehaviour/InvokeClaudeCodeBehaviour.cs:124-133`
- Test: `Calamari.AiAgent.Tests/RunAgentCommandFixture.cs` (optional integration test)

**Interfaces:**
- Consumes: `ArtifactManifestCollector.Collect(workingDir, destinationRoot)` → `IReadOnlyList<CapturedArtifact>` (Tasks 1–2); `ILog.NewOctopusArtifact(fullPath, name, fileLength)`.
- Produces: after a successful CLI run, one `NewOctopusArtifact` service message per captured artifact, emitted before the temp working directory is disposed.

- [ ] **Step 1: Add the wiring**

In `InvokeClaudeCodeBehaviour.Execute`, the run currently ends with (around lines 124–133):

```csharp
        var response = await new ClaudeCodeCliRunner(log).RunAsync(
            argsBuilder,
            environment,
            runAs,
            workingDir,
            context.CurrentDirectory,
            cancellationToken.Token);

        Log.SetOutputVariable(SpecialVariables.Action.Claude.Response, response, variables);
        log.Info("Claude Code invocation complete.");
```

Insert the artifact capture between the `RunAsync` call and `SetOutputVariable` (it runs while `tempDir` is still alive because disposal only happens when `Execute` returns):

```csharp
        var response = await new ClaudeCodeCliRunner(log).RunAsync(
            argsBuilder,
            environment,
            runAs,
            workingDir,
            context.CurrentDirectory,
            cancellationToken.Token);

        foreach (var artifact in new ArtifactManifestCollector().Collect(workingDir, context.CurrentDirectory))
            log.NewOctopusArtifact(artifact.Path, artifact.Name, artifact.Length);

        Log.SetOutputVariable(SpecialVariables.Action.Claude.Response, response, variables);
        log.Info("Claude Code invocation complete.");
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build source/Calamari.AiAgent/Calamari.AiAgent.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Add an integration test (optional, requires `ANTHROPIC_TOKEN`)**

Add to `RunAgentCommandFixture` (mirrors the existing `[Category("Integration")]` tests):

```csharp
    [Test]
    [Category("Integration")]
    public async Task ClaudeCode_AttachesArtifact_WhenExplicitlyAsked()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.Claude.SandboxMode, nameof(SandboxMode.None));
                context.Variables.Add(SpecialVariables.Action.Claude.ApiToken, Environment.GetEnvironmentVariable("ANTHROPIC_TOKEN"));
                context.Variables.Add(SpecialVariables.Action.Claude.Prompt, "Create a file named report.txt containing the word Octopus, then attach it as an Octopus artifact.");
                context.Variables.Add(SpecialVariables.Action.Claude.AllowedTools, "Write,Read,Edit");
            })
            .Execute(assertWasSuccess: true);

        result.WasSuccessful.Should().BeTrue();
        // NewOctopusArtifact emits an Info "##octopus[createArtifact ...]" service message
        // (path/name are base64-encoded, so assert on the message verb, not the file name).
        result.FullLog.Should().Contain("createArtifact");
    }
```

- [ ] **Step 4: Verify non-integration tests still pass**

Run: `dotnet test source/Calamari.AiAgent.Tests/Calamari.AiAgent.Tests.csproj --filter "TestCategory!=Integration"`
Expected: PASS (existing unit/platform-agnostic tests plus Tasks 1–3 tests).

- [ ] **Step 5: Commit**

```bash
git add source/Calamari.AiAgent/ClaudeCodeBehaviour/InvokeClaudeCodeBehaviour.cs source/Calamari.AiAgent.Tests/RunAgentCommandFixture.cs
git commit -m "Emit NewOctopusArtifact for files declared in the artifact manifest"
```

---

## Self-Review

**Spec coverage:**
- Skill trigger (always on) → Task 3. ✅
- Manifest format `.octopus/artifacts.jsonl`, JSONL, `name` optional → Tasks 1–2. ✅
- Calamari validates → copies out → emits `NewOctopusArtifact` → Tasks 1–2 (validate/copy/zip), Task 4 (emit). ✅
- Working-dir-only boundary, canonical/symlink resolution, reject root → Tasks 1–2. ✅
- Hard-fail on invalid entries (missing, malformed, empty path, outside, root, empty dir) → Tasks 1–2. ✅
- Multiple discrete files → Task 1; whole tree → zip in Task 2. ✅
- Capture to `calamariDir/artifacts/<relpath>` preserving relative path; copy not move → Tasks 1–2. ✅
- Testing matrix from the spec → covered across Tasks 1–2 fixtures; integration smoke in Task 4. ✅

**Placeholder scan:** No TBD/TODO/"add error handling" — every code and test step is concrete. ✅

**Type consistency:** `ArtifactManifestCollector.Collect(string, string) → IReadOnlyList<CapturedArtifact>` and `CapturedArtifact(Path, Name, Length)` are used identically in Tasks 1, 2, and 4. The `Capture` signature changes only within Task 2 (internal, private). ✅

**Known limitation (documented in code + spec):** symlinked *intermediate directories* are not resolved (only leaf links); listed as a design-doc follow-up.