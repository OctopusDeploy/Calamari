using System.Net;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Iam.v1;
using Google.Apis.Services;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.GoogleCloud.Accounts
{
    class GoogleCloudAccountVerifier : IVerifyAccount
    {
        public void Verify(AccountDetails account)
        {
            var accountTyped = (GoogleCloudAccountDetails) account;
            var credential = GoogleCredential.FromJson(accountTyped.JsonKey?.ToString());
            var service = new IamService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential
            });

            var dummyRequest = service.Projects.ServiceAccounts.List("projects/" + "invalidProjectName");
            try
            {
                dummyRequest.Execute();
            }
            catch (GoogleApiException exception)
            {
                if (exception.HttpStatusCode == HttpStatusCode.Unauthorized) throw;
            }
        }
    }
}