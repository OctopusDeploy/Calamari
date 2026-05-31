using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Domain;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Conventions.ManifestTemplating;

public class CopyTemplatesSourceUpdater : ISourceUpdater
{
    readonly ILog log;
    readonly IPackageRelativeFile[] packageFiles;
    readonly ICalamariFileSystem fileSystem;
    readonly bool purgeOutputDirectory;
    readonly string? overriddenPath;

    readonly string[] foldersExcludedFromPurge = [".git"];

    public CopyTemplatesSourceUpdater(IPackageRelativeFile[] packageFiles,
                                      ILog log,
                                      ICalamariFileSystem fileSystem,
                                      bool purgeOutputDirectory,
                                      string? overriddenPath = null)
    {
        this.packageFiles = packageFiles;
        this.log = log;
        this.fileSystem = fileSystem;
        this.purgeOutputDirectory = purgeOutputDirectory;
        this.overriddenPath = overriddenPath;
    }

    public FileUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory)
    {
        if (!TryCalculateOutputPath(sourceWithMetadata.Source, out var outputPath))
        {
            return new FileUpdateResult([], [], [], []);
        }
        log.VerboseFormat("Copying files into '{0}'", outputPath);

        var workingDirectoryPath = Path.Combine(workingDirectory, outputPath);

        var purgedFiles = purgeOutputDirectory ? PurgeFilesIn(workingDirectoryPath) : []; 
        
        var filesToCopy = packageFiles.Select(f => new FileCopySpecification(f, workingDirectory, outputPath)).ToList();
        CopyFiles(filesToCopy);
        
        var fileHashes = filesToCopy.Select(f => new FileHash(f.DestinationRelativePath, HashCalculator.Hash(f.DestinationAbsolutePath)))
                                    .ToList();
        return new FileUpdateResult([], fileHashes, [], purgedFiles.Select(pf => Path.GetRelativePath(workingDirectory, pf)).ToArray());
    }
    
    bool TryCalculateOutputPath(ApplicationSource sourceToUpdate, out string outputPath)
    {
        if (!overriddenPath.IsNullOrEmpty())
        {
            outputPath = overriddenPath;
            return true;
        }
        
        outputPath = "";
        var sourceIdentity = string.IsNullOrEmpty(sourceToUpdate.Name) ? sourceToUpdate.OriginalRepoUrl : sourceToUpdate.Name;
        if (sourceToUpdate.Ref != null)
        {
            if (sourceToUpdate.Path != null)
            {
                log.WarnFormat("Unable to update ref source '{0}' as a path has been explicitly specified.", sourceIdentity);
                log.Warn("Please split the source into separate sources and update annotations.");
                return false;
            }

            return true;
        }

        if (sourceToUpdate.Path == null)
        {
            log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceIdentity);
            return false;
        }

        outputPath = sourceToUpdate.Path;
        return true;
    }

    string[] PurgeFilesIn(string outputDirectory)
    {
        log.Info($"Removing files recursively from {outputDirectory}");
        
        var excludedPaths = foldersExcludedFromPurge.Select(excludedFolder => Path.Combine(outputDirectory, excludedFolder)).ToList();
        var filesToRemove = fileSystem.EnumerateFilesRecursively(outputDirectory)
                                     .Where(f => !excludedPaths.Any(f.StartsWith))
                                     .ToArray();

        foreach (var file in filesToRemove)
        {
            fileSystem.DeleteFile(file);
        }
        
        return filesToRemove;
    }
    
    void CopyFiles(IEnumerable<IFileCopySpecification> repositoryFiles)
    {
        foreach (var file in repositoryFiles)
        {
            log.VerboseFormat($"Copying '{file.SourceAbsolutePath}' to '{file.DestinationAbsolutePath}'");
            EnsureParentDirectoryExists(file.DestinationAbsolutePath);
            fileSystem.CopyFile(file.SourceAbsolutePath, file.DestinationAbsolutePath);
        }
    }
    
    void EnsureParentDirectoryExists(string filePath)
    {
        var destinationDirectory = Path.GetDirectoryName(filePath);
        if (destinationDirectory != null)
        {
            fileSystem.CreateDirectory(destinationDirectory);
        }
    }
}
