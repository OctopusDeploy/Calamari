namespace Calamari.ConsolidatedPackagesCommon;

public class FileBasedStreamProvider : IConsolidatedPackageStreamProvider
{
    readonly string filename;

    public FileBasedStreamProvider(string filename)
    {
        this.filename = filename;
    }

    public Stream OpenStream()
    {
        return File.OpenRead(filename);
    }
}