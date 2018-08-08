namespace Calamari.Shared
{
    public interface ICommandResult
    {
        int ExitCode { get; }

        string Errors { get; }

        bool HasErrors { get; }

        ICommandResult VerifySuccess();
    }
}