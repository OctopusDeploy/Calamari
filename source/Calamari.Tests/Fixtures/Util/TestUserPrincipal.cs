using System;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Net;
using System.Security.Principal;
using Microsoft.Win32;
using Polly;

namespace Calamari.Tests.Fixtures.Util
{
    public class TestUserPrincipal
    {
        public TestUserPrincipal(string username, string password = null)
        {
            var usernameToUse = username;
            if (usernameToUse.Length > 20)
            {
                var shortenedUsername = new string(usernameToUse.Take(20).ToArray());
                Console.WriteLine($"The requested username '{usernameToUse}' will fail because it is longer than 20 characters. Shortening it to '{shortenedUsername}' instead. You should always use the resulting Username property of this class when trying to use the user account.");
                usernameToUse = shortenedUsername;
            }

            try
            {
                var usingRandomlyGeneratedPassword = password == null;
                if (usingRandomlyGeneratedPassword)
                {
                    // There is a slight chance the password won't meet complexity requirements, try a few times
                    string passwordToUse = null;
                    Policy.Handle<PasswordException>().Retry(
                            retryCount: 10,
                            onRetry: (exception, i) => { Console.WriteLine($"The password '{passwordToUse}' was not acceptable: {exception.Message}. Trying another random password!"); })
                        .Execute(() =>
                        {
                            passwordToUse = PasswordGenerator.Generate(16, 4);
                            CreateOrUpdateUser(usernameToUse, passwordToUse);
                        });
                }
                else
                {
                    CreateOrUpdateUser(usernameToUse, password);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create the Windows User Account called '{username}': {ex.Message}");
                throw;
            }
       
        }

        void CreateOrUpdateUser(string username, string password)
        {
            using (var principalContext = new PrincipalContext(ContextType.Machine))
            {
                UserPrincipal principal = null;

                try
                {
                    principal = UserPrincipal.FindByIdentity(principalContext, IdentityType.Name, username);
                    if (principal != null)
                    {
                        Console.WriteLine($"The Windows User Account named '{username}' already exists, making sure the password is set correctly...");
                        principal.SetPassword(password);
                        principal.Save();
                    }
                    else
                    {
                        Console.WriteLine($"Trying to create a Windows User Account on the local machine called '{username}'...");
                        principal = new UserPrincipal(principalContext) { Name = username };
                        principal.SetPassword(password);
                        principal.Save();
                    }
                    
                    HideUserAccountFromLogonScreen(username);

                    SamAccountName = principal.SamAccountName;
                    Sid = principal.Sid;
                    Password = password;
                }
                finally
                {
                    principal?.Dispose();
                }
            }
        }

        static void HideUserAccountFromLogonScreen(string username)
        {
#pragma warning disable CA1416
            using (var winLogonSubKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CUrrentVersion\\WinLogon", RegistryKeyPermissionCheck.ReadWriteSubTree))

            {
                using (var specialAccountsSubKey = winLogonSubKey.GetSubKeyNames().Contains("SpecialAccounts")
                    ? winLogonSubKey.OpenSubKey("SpecialAccounts", RegistryKeyPermissionCheck.ReadWriteSubTree)
                    : winLogonSubKey.CreateSubKey("SpecialAccounts"))
                {
                    using (var userListSubKey = specialAccountsSubKey.GetSubKeyNames().Contains("UserList")
                        ? specialAccountsSubKey.OpenSubKey("UserList", RegistryKeyPermissionCheck.ReadWriteSubTree)
                        : specialAccountsSubKey.CreateSubKey("UserList"))
                    {
                        userListSubKey.SetValue(username, 0, RegistryValueKind.DWord);
                    }
                }
            }
#pragma warning restore CA1416
        }

        public TestUserPrincipal EnsureIsMemberOfGroup(string groupName)
        {
            Console.WriteLine($"Ensuring the Windows User Account called '{UserName}' is a member of the '{groupName}' group...");
            using (var principalContext = new PrincipalContext(ContextType.Machine))
#pragma warning disable CA1416
            using (var principal = UserPrincipal.FindByIdentity(principalContext, IdentityType.Sid, Sid.Value))

            {
                if (principal == null) throw new Exception($"Couldn't find a user account for {UserName} by the SID {Sid.Value}");
                using (var group = GroupPrincipal.FindByIdentity(principalContext, IdentityType.Name, groupName))
                {
                    if (group == null) throw new Exception($"Couldn't find a group with the name {groupName}");
                    if (!group.Members.Contains(principal))
                    {
                        group.Members.Add(principal);
                        group.Save();
                    }
                }

                return this;
            }
#pragma warning restore CA1416
        }

        public TestUserPrincipal GrantLogonAsAServiceRight()
        {
            var privilegeName = "SeServiceLogonRight";
            Console.WriteLine($"Granting the '{privilegeName}' privilege to the '{NTAccountName}' user account.");
            LsaUtility.SetRight(NTAccountName, privilegeName);
            return this;
        }
        
        public SecurityIdentifier Sid { get; private set; }

#pragma warning disable CA1416 // API not supported on all platforms
        public string NTAccountName => Sid.Translate(typeof(NTAccount)).ToString();
#pragma warning restore CA1416 // API not supported on all platforms
        public string DomainName => NTAccountName.Split(new[] {'\\'}, 2)[0];
        public string UserName => NTAccountName.Split(new[] {'\\'}, 2)[1];
        public string SamAccountName { get; private set; }
        public string Password { get; private set; }

        public NetworkCredential GetCredential() => new NetworkCredential(UserName, Password, DomainName);

        public override string ToString()
        {
            return NTAccountName;
        }

        public void Delete()
        {
            using (var principalContext = new PrincipalContext(ContextType.Machine))
            {
                UserPrincipal principal = null;

                try
                {
                    principal = UserPrincipal.FindByIdentity(principalContext, IdentityType.Name, UserName);
                    if (principal == null)
                    {
                        Console.WriteLine($"The Windows User Account named {UserName} doesn't exist, nothing to do...");
                        return;
                    }
                    Console.WriteLine($"The Windows User Account named {UserName} exists, deleting...");
                    principal.Delete();
                }
                finally
                {
                    principal?.Dispose();
                }
            }
        }
    }
}
