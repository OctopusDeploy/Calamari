using System;
using System.Net.Http;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;

namespace Sashimi.Azure.Web
{
    class AzureApi : RegisterEndpoint
    {
        public const string AzureEnvironmentsPath = "/api/accounts/azureenvironments";

        public AzureApi(Func<SecuredAsyncActionInvoker<AzureEnvironmentsListAction>> azureEnvironmentsListInvokerFactory)
        {
            Add(HttpMethod.Get.ToString(), AzureEnvironmentsPath, azureEnvironmentsListInvokerFactory().ExecuteAsync);
        }
    }
}
