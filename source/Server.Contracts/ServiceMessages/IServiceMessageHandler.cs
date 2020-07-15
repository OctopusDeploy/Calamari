namespace Sashimi.Server.Contracts.Accounts
{
    public interface IServiceMessageHandler
    {
        string AuditEntryDescription { get; }
        string ServiceMessageName { get; }
    }
}