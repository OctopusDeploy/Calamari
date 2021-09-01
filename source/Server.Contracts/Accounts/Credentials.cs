using System;

namespace Sashimi.Server.Contracts.Accounts
{
    public class Credentials
    {
        public Credentials(string userName, string password)
        {
            UserName = userName;
            Password = password;
        }

        public Credentials(string password)
        {
            Password = password;
        }

        public string? UserName { get; }
        public string Password { get; }
    }
}