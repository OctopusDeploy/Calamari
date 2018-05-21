namespace Calamari.Integration.ConfigurationTransforms
{
    public interface IConfigurationTransformer
    {
        void PerformTransform(string configFile, string transformFile, string destinationFile);
    }
}