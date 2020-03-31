using SharpCompress.Common;

namespace Calamari.Integration.Packages
{
    public class PackageExtractionOptions : ExtractionOptions
    {
        public PackageExtractionOptions()
        {
            ExtractFullPath = true;
            Overwrite = true;
            PreserveFileTime = true;
            WriteSymbolicLink = WarnThatSymbolicLinksAreNotSupported;
        }

        static void WarnThatSymbolicLinksAreNotSupported(string sourcepath, string targetpath)
            => Log.WarnFormat("Cannot create symbolic link: {0}, Calamari does not currently support the extraction of symbolic links", sourcepath);
    }
}