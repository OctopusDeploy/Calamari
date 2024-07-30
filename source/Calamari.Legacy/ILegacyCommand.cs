namespace Calamari.Legacy
{
    public interface ILegacyCommand
    {
        string Name { get; }

        void Execute(string[] args);
    }
}