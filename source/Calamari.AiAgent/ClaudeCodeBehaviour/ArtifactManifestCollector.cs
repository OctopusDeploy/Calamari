using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public record CapturedArtifact(string Path, string Name, long Length);

public class ArtifactManifestCollector(IVariables variables)
{
    const string ArtifactsDirName = "artifacts";

    public const long DefaultMaxArtifactSizeBytes = 5L * 1024 * 1024 * 1024; // 5Gb

    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<CapturedArtifact> Collect(string workingDir, string destinationRoot)
    {
        var manifestPath = Path.Combine(workingDir, ".octopus", "artifacts.jsonl");
        if (!File.Exists(manifestPath))
            return Array.Empty<CapturedArtifact>();

        var maxTotalBytes = ResolveMaxTotalBytes();
        var canonicalWorkingDir = Canonical(workingDir);
        var artifactsDir = Path.Combine(destinationRoot, ArtifactsDirName);

        // Not transactional: an invalid entry throws after earlier ones were copied out.
        // The caller only emits NewOctopusArtifact for a fully-returned list, so no orphan is ever registered.
        var captured = new List<CapturedArtifact>();
        var totalBytes = 0L;
        var lineNumber = 0;
        foreach (var rawLine in File.ReadAllLines(manifestPath))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var entry = ParseEntry(line, lineNumber);
            var artifact = Capture(entry, lineNumber, workingDir, canonicalWorkingDir, artifactsDir);

            totalBytes += artifact.Length;
            if (totalBytes > maxTotalBytes)
                throw new CommandException(
                    $"Artifacts exceed the maximum total upload size of {maxTotalBytes.ToFileSizeString()} ({totalBytes.ToFileSizeString()} so far). "
                    + $"Increase '{SpecialVariables.Action.Claude.MaxArtifactSizeInMegaBytes}' (in megabytes) to allow more.");

            captured.Add(artifact);
        }

        return captured;
    }

    long ResolveMaxTotalBytes()
    {
        var maxSizeMb = variables.GetInt32(SpecialVariables.Action.Claude.MaxArtifactSizeInMegaBytes);
        return maxSizeMb.HasValue ? (long)maxSizeMb.Value * 1024 * 1024 : DefaultMaxArtifactSizeBytes;
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
