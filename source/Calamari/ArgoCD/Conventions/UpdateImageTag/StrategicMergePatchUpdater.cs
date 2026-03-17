using System.Collections.Generic;
using System.IO;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag
{
    public class StrategicMergePatchUpdater : BasePatchUpdater
    {
        readonly string patchFilePath;

        public StrategicMergePatchUpdater(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
                                          string defaultRegistry,
                                          string patchFilePath,
                                          ILog log,
                                          ICalamariFileSystem fileSystem)
            : base(imagesToUpdate, defaultRegistry, log, fileSystem)
        {
            this.patchFilePath = patchFilePath;
        }

        public override ImageReplacementResult ReplaceImages(string input)
        {
            var imageReplacer = new StrategicMergePatchImageReplacer(input, defaultRegistry, log);
            return imageReplacer.UpdateImages(imagesToUpdate);
        }

        protected override FileUpdateResult ProcessSpecificPatchFiles(string rootPath, string subFolder)
        {
            if (!fileSystem.FileExists(patchFilePath))
            {
                log.WarnFormat("Strategic merge patch file not found: {0}", patchFilePath);
                return new FileUpdateResult([], []);
            }

            var filesToUpdate = new HashSet<string> { patchFilePath };
            log.Verbose($"Processing strategic merge patch file: {Path.GetRelativePath(rootPath, patchFilePath)}");
            return Update(rootPath, filesToUpdate);
        }
    }
}