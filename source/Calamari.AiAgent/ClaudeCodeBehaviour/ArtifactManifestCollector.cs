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
