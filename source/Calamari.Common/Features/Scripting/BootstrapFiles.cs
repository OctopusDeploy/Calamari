namespace Calamari.Common.Features.Scripting
{
    public class BootstrapFiles
    {
        public string BootstrapFile { get; }
        public string[] TemporaryFiles { get; }

        public BootstrapFiles(string bootstrapFile, string[] temporaryFiles)
        {
            BootstrapFile = bootstrapFile;
            TemporaryFiles = temporaryFiles;
        }
    }
}