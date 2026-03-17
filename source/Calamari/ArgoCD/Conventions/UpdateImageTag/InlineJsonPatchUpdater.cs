using System.Collections.Generic;
using System.IO;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag
{
    public class InlineJsonPatchUpdater : BasePatchUpdater
    {
        readonly string kustomizationFilePath;

        public InlineJsonPatchUpdater(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
                                      string defaultRegistry,
                                      string kustomizationFilePath,
                                      ILog log,
                                      ICalamariFileSystem fileSystem)
            : base(imagesToUpdate, defaultRegistry, log, fileSystem)
        {
            this.kustomizationFilePath = kustomizationFilePath;
        }

        public override ImageReplacementResult ReplaceImages(string input)
        {
            var imageReplacer = new InlineJsonPatchImageReplacer(input, defaultRegistry, log);
            return imageReplacer.UpdateImages(imagesToUpdate);
        }

        protected override FileUpdateResult ProcessSpecificPatchFiles(string rootPath, string subFolder)
        {
            if (!fileSystem.FileExists(kustomizationFilePath))
            {
                log.WarnFormat("Kustomization file not found: {0}", kustomizationFilePath);
                return new FileUpdateResult([], []);
            }

            var filesToUpdate = new HashSet<string> { kustomizationFilePath };
            log.Verbose($"Processing inline patches in kustomization file: {Path.GetRelativePath(rootPath, kustomizationFilePath)}");
            return Update(rootPath, filesToUpdate);
        }
    }
}