namespace Calamari.Extensibility.Features
{
    public interface ICalamariContainer
    {
        void RegisterInstance<TType>(TType instance);
    }


    public interface IModule
    {
        void Register(ICalamariContainer container);
    }
}