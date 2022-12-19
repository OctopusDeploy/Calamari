using SharpCompress.Archives;

namespace Calamari.Common.Features.Packages.Decorators
{
    /// <summary>
    /// A base Decorator which allows addition to the behaviour of an IPackageEntryExtractor 
    /// </summary>
    /// <remarks>
    /// IPackageEntryExtractor is a more specific IPackageExtractor, which provides a hook to the extraction
    /// of each individual ArchiveEntry.
    /// </remarks>
    public class PackageEntryExtractorDecorator : IPackageEntryExtractor
    {
        readonly IPackageEntryExtractor concreteEntryExtractor;

        protected PackageEntryExtractorDecorator(IPackageEntryExtractor concreteEntryExtractor)
        {
            this.concreteEntryExtractor = concreteEntryExtractor;
        }

        public virtual int Extract(string packageFile, string directory)
        {
            return concreteEntryExtractor.Extract(packageFile, directory);
        }

        public virtual void ExtractEntry(string directory, IArchiveEntry entry)
        {
            concreteEntryExtractor.ExtractEntry(directory, entry);
        }

        public string[] Extensions => concreteEntryExtractor.Extensions;
    }
}
