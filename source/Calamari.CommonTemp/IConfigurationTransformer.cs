namespace Calamari.CommonTemp
{
    public interface IConfigurationTransformer
    {
        void PerformTransform(string configFile, string transformFile, string destinationFile);
    }
}