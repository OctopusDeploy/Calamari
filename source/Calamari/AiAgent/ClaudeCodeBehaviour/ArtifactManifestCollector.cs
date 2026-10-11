using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public record StagedArtifact(string Path, string Name, long Length);

public class ArtifactManifestCollector(IVariables variables, IReadOnlyCollection<string>? secretValues = null)
{
    const string ArtifactsDirName = "artifacts";

    // Shorter values match too much ordinary content to be treated as secrets
    const int MinSecretLength = 8;

    // Files the step writes for Claude. These hold config and secrets, so they are never uploaded.
    static readonly string[] StepOwnedPaths =
    [
        McpWriter.ConfigName,
        InvokeClaudeCodeBehaviour.ClaudeConfigDirName,
        InvokeClaudeCodeBehaviour.DeploymentVariablesFileName,
        SandboxSettingsWriter.SandboxRuntimeSettingsFileName,
    ];

    readonly string[] secrets = (secretValues ?? [])
                                .Where(value => value.Length >= MinSecretLength)
                                .Distinct(StringComparer.Ordinal)
                                .ToArray();

    public const long DefaultMaxArtifactSizeBytes = 5L * 1024 * 1024 * 1024; // 5Gb

    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<StagedArtifact> Collect(string workingDir, string destinationRoot)
    {
        var manifestPath = Path.Combine(workingDir, ".octopus", "artifacts.jsonl");
        if (!File.Exists(manifestPath))
            return Array.Empty<StagedArtifact>();

        var maxTotalBytes = ResolveMaxTotalBytes();
        var canonicalWorkingDir = Canonical(workingDir);
        var artifactsDir = Path.Combine(destinationRoot, ArtifactsDirName);

        // Not transactional: an invalid entry throws after earlier ones were copied out.
        // The caller only emits NewOctopusArtifact for a fully-returned list, so no orphan is ever registered.
        var staged = new List<StagedArtifact>();
        var totalBytes = 0L;
        var lineNumber = 0;
        foreach (var rawLine in File.ReadAllLines(manifestPath))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var entry = ParseEntry(line, lineNumber);
            var artifact = Stage(entry, lineNumber, workingDir, canonicalWorkingDir, artifactsDir);

            totalBytes += artifact.Length;
            if (totalBytes > maxTotalBytes)
                throw new CommandException(
                    $"Artifacts exceed the maximum total upload size of {maxTotalBytes.ToFileSizeString()} ({totalBytes.ToFileSizeString()} so far). "
                    + $"Increase '{SpecialVariables.Action.Claude.MaxArtifactSizeInMegaBytes}' (in megabytes) to allow more.");

            staged.Add(artifact);
        }

        return staged;
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

    StagedArtifact Stage(ManifestEntry entry, int lineNumber, string workingDir, string canonicalWorkingDir, string artifactsDir)
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
        if (StepOwnedPaths.Select(path => Path.Combine(canonicalWorkingDir, path)).Any(stepOwned => Overlaps(canonical, stepOwned)))
            throw new CommandException($"Artifact manifest line {lineNumber}: '{entry.Path}' is a file the step writes for Claude and cannot be attached.");

        var relative = Path.GetRelativePath(workingDir, full);

        return isDirectory
            ? StageDirectory(entry, lineNumber, full, relative, artifactsDir)
            : StageFile(entry, lineNumber, full, relative, artifactsDir);
    }

    StagedArtifact StageFile(ManifestEntry entry, int lineNumber, string full, string relative, string artifactsDir)
    {
        EnsureNoSecrets(full, entry, lineNumber);

        var destPath = Path.Combine(artifactsDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.Copy(full, destPath, overwrite: true);

        var name = entry.Name ?? Path.GetFileName(full);
        return new StagedArtifact(destPath, name, new FileInfo(destPath).Length);
    }

    StagedArtifact StageDirectory(ManifestEntry entry, int lineNumber, string full, string relative, string artifactsDir)
    {
        if (Directory.GetFileSystemEntries(full).Length == 0)
            throw new CommandException($"Artifact manifest line {lineNumber}: directory '{entry.Path}' is empty.");

        // Zipping follows links, which would bypass the working directory and step-owned file checks
        var contents = new DirectoryInfo(full).EnumerateFileSystemInfos("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 });
        foreach (var item in contents)
        {
            if (item.LinkTarget is not null)
                throw new CommandException($"Artifact manifest line {lineNumber}: directory '{entry.Path}' contains a symbolic link '{Path.GetRelativePath(full, item.FullName)}'.");
            if (item is FileInfo file)
                EnsureNoSecrets(file.FullName, entry, lineNumber);
        }

        var zipPath = Path.Combine(artifactsDir, relative + ".zip");
        Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
        if (File.Exists(zipPath))
            File.Delete(zipPath);
        ZipFile.CreateFromDirectory(full, zipPath);

        var dirName = new DirectoryInfo(full).Name;
        var name = entry.Name ?? dirName + ".zip";
        return new StagedArtifact(zipPath, name, new FileInfo(zipPath).Length);
    }

    // Case-insensitive so a differently-cased path on a case-insensitive file system still matches
    static bool Overlaps(string path, string stepOwned)
        => string.Equals(path, stepOwned, StringComparison.OrdinalIgnoreCase)
           || path.StartsWith(stepOwned + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
           || stepOwned.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    void EnsureNoSecrets(string path, ManifestEntry entry, int lineNumber)
    {
        if (secrets.Length > 0 && ContainsAny(path, secrets))
            throw new CommandException($"Artifact manifest line {lineNumber}: '{entry.Path}' contains a sensitive value and cannot be attached.");
    }

    // Reads in chunks and carries the tail of each chunk forward, so a secret split across chunks is still found
    static bool ContainsAny(string path, string[] values)
    {
        var overlap = values.Max(value => value.Length) - 1;
        var buffer = new char[81920];
        var carry = "";
        using var reader = new StreamReader(path);
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            var window = carry + new string(buffer, 0, read);
            if (values.Any(value => window.Contains(value, StringComparison.Ordinal)))
                return true;
            carry = window.Length > overlap ? window[^overlap..] : window;
        }

        return false;
    }

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
