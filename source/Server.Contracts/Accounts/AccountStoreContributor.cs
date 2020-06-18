namespace Sashimi.Server.Contracts.Accounts
{
    public abstract class AccountStoreContributor
    {
        public virtual bool CanContribute(AccountDetailsResource resource)
        {
            return false;
        }

        public virtual ValidationResult ValidateResource(AccountDetailsResource resource)
        {
            return ValidationResult.Success;
        }

        public virtual void ModifyResource(AccountDetailsResource accountResource, string name)
        {
        }

        public virtual void ModifyModel(AccountDetailsResource resource, AccountDetails model, string name)
        {
        }
    }

    public class ValidationResult
    {
        public static readonly ValidationResult Success = new ValidationResult(true, null);

        public static ValidationResult Error(string errorMessage)
        {
            return new ValidationResult(false, errorMessage);
        }

        ValidationResult(bool isValid, string? errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        public bool IsValid { get; }
        public string? ErrorMessage { get; }
    }
}