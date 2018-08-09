namespace Calamari.Commands.Support
{
    public interface ICommandMetadata
    {
        string Name { get; }
//        string[] Aliases { get; }
        string Description { get; }
    }
}