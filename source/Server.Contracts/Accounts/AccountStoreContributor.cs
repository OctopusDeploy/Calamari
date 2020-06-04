namespace Sashimi.Server.Contracts.Accounts
{
    public abstract class AccountStoreContributor
    {
        public virtual bool CanContribute(AccountDetailsResource resource)
        {
            return false;
        }

        public virtual bool ValidateResource(AccountDetailsResource resource, out string errorMessage)
        {
            errorMessage = null!;
            return true;
        }

        public virtual void ModifyResource(AccountDetailsResource accountResource, string name)
        {
        }

        public virtual void ModifyModel(AccountDetailsResource resource, AccountDetails model, string name)
        {
        }
    }
}