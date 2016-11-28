namespace Calamari.Extensibility.Features
{
    public interface IRollBackFeature
    {
        void Rollback(IVariableDictionary variables);
        void Cleanup(IVariableDictionary variables);
    }
}