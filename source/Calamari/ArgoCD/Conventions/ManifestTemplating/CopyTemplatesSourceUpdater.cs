using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Domain;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.ManifestTemplating;

public class CopyTemplatesSourceUpdater : ISourceUpdater
{
    readonly ILog log;
    readonly IPackageRelativeFile[] packageFiles;
    readonly ArgoCommitToGitConfig deploymentConfig;
    readonly ICalamariFileSystem fileSystem;
    readonly bool purgeOutputDirectory;

    public CopyTemplatesSourceUpdater(IPackageRelativeFile[] packageFiles, ILog log, ArgoCommitToGitConfig deploymentConfig, ICalamariFileSystem fileSystem,
                                      bool purgeOutputDirectory)
    {
        this.packageFiles = packageFiles;
        this.log = log;
        this.deploymentConfig = deploymentConfig;
        this.fileSystem = fileSystem;
        this.purgeOutputDirectory = purgeOutputDirectory;
    }

    public FileUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory)
    {
        if (!TryCalculateOutputPath(sourceWithMetadata.Source, out var outputPath))
        {
            return new FileUpdateResult([], []);
        }
        log.VerboseFormat("Copying files into '{0}'", outputPath);

        var workingDirectoryPath = Path.Combine(workingDirectory, outputPath);

        var deletedFiles = new List<string>();
        if (deploymentConfig.PurgeOutputDirectory)
        {
            deletedFiles.AddRange(PurgeFilesIn(workingDirectoryPath));
        }
        
        var filesToCopy = packageFiles.Select(f => new FileCopySpecification(f, workingDirectory, outputPath)).ToList();
        CopyFiles(filesToCopy);
        
        var fileHashes = filesToCopy.Select(f => new FilePathContent(
                                                                     // Replace \ with / so that Calamari running on windows doesn't cause issues when we send back to server
                                                                     f.DestinationRelativePath.Replace('\\', '/'),
                                                                     HashCalculator.Hash(f.DestinationAbsolutePath)))
                                    .ToList();
        return new FileUpdateResult([], fileHashes, deletedFiles.ToArray());
    }
    
    bool TryCalculateOutputPath(ApplicationSource sourceToUpdate, out string outputPath)
    {
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
        var cleansedSubPath = NormalizePath(outputDirectory);
        if (!cleansedSubPath.EndsWith(Path.DirectorySeparatorChar) && !cleansedSubPath.IsNullOrEmpty())
        {
            cleansedSubPath += Path.DirectorySeparatorChar;
        }
        log.Info("Removing files recursively");
        
        var filesInSpace = fileSystem.EnumerateFilesRecursively(cleansedSubPath);
        
        fileSystem.DeleteDirectory(cleansedSubPath);

        return filesInSpace.ToArray();

    }
    
    static string NormalizePath(string path)
    {
        var separatorToReplace = Path.DirectorySeparatorChar == '/' ? '\\' : '/';
        var normalized = path.Replace(separatorToReplace, Path.DirectorySeparatorChar);
        return normalized.StartsWith($".{Path.DirectorySeparatorChar}") ? normalized.Substring(2) : normalized;
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