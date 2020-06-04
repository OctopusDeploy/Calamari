namespace Sashimi.Server.Contracts.Accounts
{
    public interface IVerifyAccount
    {
        void Verify(AccountDetails account);
    }
}