namespace Calamari.Extensibility.Features
{
    public interface ICalamariContainer
    {
        void RegisterInstance<TType>(TType instance);

        TType Resolve<TType>();
    }


    public interface IModule
    {
        void Register(ICalamariContainer container);
    }
}