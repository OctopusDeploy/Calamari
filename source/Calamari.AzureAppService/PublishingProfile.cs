using System;
using System.Text;

namespace Calamari.AzureAppService
{
    public class PublishingProfile
    {
        public string Site { get; set; }

        public string Password { get; set; }

        public string Username { get; set; }

        public string PublishUrl { get; set; }
        
        public string GetBasicAuthCredentials()
            => Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Username}:{Password}"));
    }
}
