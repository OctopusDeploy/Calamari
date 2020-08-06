namespace Sashimi.Server.Contracts.ServiceMessages
{
    public interface IServiceMessageHandler
    {
        string AuditEntryDescription { get; }
        string ServiceMessageName { get; }
    }
}