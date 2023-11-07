using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Calamari.CloudAccounts;

namespace Calamari.AzureAppService
{
    public interface IBasicAuthService
    {
        Task<string> GetBasicAuthCreds(IAzureAccount azureAccount, AzureTargetSite targetSite, CancellationToken cancellationToken);
    }
    
    public class BasicAuthService : IBasicAuthService
    {
        readonly IPublishingProfileService publishingProfileService;

        public BasicAuthService(IPublishingProfileService publishingProfileService)
        {
            this.publishingProfileService = publishingProfileService;
        }
        
        public async Task<string> GetBasicAuthCreds(IAzureAccount azureAccount, AzureTargetSite targetSite, CancellationToken cancellationToken)
        {
            var publishingProfile = await publishingProfileService.GetPublishingProfile(targetSite, azureAccount);
            var basicAuth = $"{publishingProfile.Username}:{publishingProfile.Password}";
            var credential = Convert.ToBase64String(Encoding.ASCII.GetBytes(basicAuth));
            return credential;
        }
    }
}