
namespace Calamari.Extensibility.Scripting
{
    public interface ICommandResult
    {
        int ExitCode { get; }
        string Errors { get; }
    }
}
