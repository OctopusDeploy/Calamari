namespace Calamari.Features
{
    public interface IFeatureLocator
    {
        FeatureExtension Locate(string name);
    }
}