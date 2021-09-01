using System;
using Octopus.TinyTypes;

namespace Sashimi.Server.Contracts.Accounts
{
    public class AccountType : CaseInsensitiveStringTinyType
    {
        public static readonly AccountType None = new(nameof(None));

        public AccountType(string value) : base(value)
        {
        }
    }
}