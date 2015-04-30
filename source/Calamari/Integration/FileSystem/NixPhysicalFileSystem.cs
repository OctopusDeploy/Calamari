namespace Calamari.Integration.FileSystem
{
    public class NixCalamariPhysicalFileSystem : CalamariPhysicalFileSystem
    {
        protected override bool GetFiskFreeSpace(string directoryPath, out ulong totalNumberOfFreeBytes)
        {
            totalNumberOfFreeBytes = 0;
            return false;
        }
    }
}
