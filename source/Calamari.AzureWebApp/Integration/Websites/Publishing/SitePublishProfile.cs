using System;

namespace Calamari.AzureWebApp.Integration.Websites.Publishing
{
    public class SitePublishProfile
    {
        public SitePublishProfile(string userName, string password, Uri uri)
        {
            UserName = userName;
            Password = password;
            Uri = uri;
        }

        public string UserName { get; }
        public string Password { get; }
        public Uri Uri { get; }
    }
}