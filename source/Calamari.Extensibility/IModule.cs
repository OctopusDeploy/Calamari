namespace Calamari.Extensibility
{
    public interface IModule
    {
        void Register(ICalamariContainer container);
    }
}