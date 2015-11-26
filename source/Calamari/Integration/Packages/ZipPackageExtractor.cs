using ICSharpCode.SharpZipLib.Zip;


namespace Calamari.Integration.Packages
{
    public class ZipPackageExtractor : SimplePackageExtractor
    {
        public override string[] Extensions { get { return new [] { ".zip"}; } }

        public override int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            var fileExtractionCount = 0;
            var fastZip = new FastZip(new FastZipEvents()
            {
                CompletedFile = (sender, args) => fileExtractionCount++
            });
            fastZip.ExtractZip(packageFile, directory, string.Empty);
            return fileExtractionCount;
        }

    }
}