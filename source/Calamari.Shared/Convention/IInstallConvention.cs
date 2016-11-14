namespace Calamari.Shared.Convention
{
    public interface IInstallConvention : IConvention
    {
        void Install(IVariableDictionary variables);
    }

    public interface IRollbackConvention2 : IConvention
    {
        void Rollback(IVariableDictionary variables);
    }
}