using System;

namespace Calamari.ConsolidateCalamariPackages
{
    class SourceFile
    {
        public string PackageId { get; set; }
        public string Version { get; set; }
        public string Platform { get; set; }
        public string ArchivePath { get; set; }
        public bool IsNupkg { get; set; }
        public string FullNameInDestinationArchive { get; set; }
        public string FullNameInSourceArchive { get; set; }
        public string Hash { get; set; }

        public string FileName { get; set; }
        public string EntryNameInConsolidationArchive() {
            return $"{Hash}/{FileName}";
        }
    }
}