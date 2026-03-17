using System.Collections.Generic;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag
{
    /// <summary>
    /// Base class for updaters that process specific patch file types.
    /// </summary>
    public abstract class BasePatchUpdater : BaseUpdater
    {
        protected readonly IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate;
        protected readonly string defaultRegistry;

        protected BasePatchUpdater(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
                                   string defaultRegistry,
                                   ILog log,
                                   ICalamariFileSystem fileSystem) : base(log, fileSystem)
        {
            this.imagesToUpdate = imagesToUpdate;
            this.defaultRegistry = defaultRegistry;
        }

        public override FileUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory)
        {
            var applicationSource = sourceWithMetadata.Source;

            if (applicationSource.Path == null)
            {
                log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceWithMetadata.SourceIdentity);
                return new FileUpdateResult([], []);
            }

            log.Verbose($"Processing patch files from {applicationSource.Path}");
            return ProcessPatchFiles(workingDirectory, applicationSource.Path);
        }

        public FileUpdateResult ProcessPatchFiles(string rootPath, string subFolder)
        {
            return ProcessSpecificPatchFiles(rootPath, subFolder);
        }

        protected abstract FileUpdateResult ProcessSpecificPatchFiles(string rootPath, string subFolder);
    }
}