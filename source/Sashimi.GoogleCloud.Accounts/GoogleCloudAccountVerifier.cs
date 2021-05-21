using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Iam.v1;
using Google.Apis.Services;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.GoogleCloud.Accounts
{
    internal class GoogleCloudAccountVerifier : IVerifyAccount
    {
        public async Task Verify(AccountDetails account, CancellationToken token)
        {
            var accountTyped = (GoogleCloudAccountDetails) account;
            if (accountTyped.JsonKey == null)
            {
                throw new Exception("Invalid credentials specified.");
            }

            var bytes = Convert.FromBase64String(accountTyped.JsonKey.Value);
            var json = Encoding.UTF8.GetString(bytes);
            GoogleCredential? credential;
            try
            {
                credential = GoogleCredential.FromJson(json);
            }
            catch (InvalidOperationException)
            {
                throw new Exception("Error reading json key file, please ensure file is correct.");
            }

            using var service = new IamService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential
            });

            var dummyRequest = service.Projects.ServiceAccounts.List("projects/" + "invalidProjectName");
            try
            {
                await dummyRequest.ExecuteAsync(token);
            }
            catch (GoogleApiException exception)
            {
                if (exception.HttpStatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new Exception("Invalid credentials specified.");
                }
            }
        }
    }
}