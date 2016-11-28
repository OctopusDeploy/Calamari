namespace Calamari.Extensibility
{
    public interface ICalamariContainer
    {
        void RegisterInstance<TType>(TType instance);

        TType Resolve<TType>();
    }
}