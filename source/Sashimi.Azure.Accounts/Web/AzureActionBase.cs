using System;
using System.Threading.Tasks;
using Microsoft.Rest.Azure;
using Octopus.Diagnostics;

namespace Sashimi.Azure.Accounts.Web
{
    abstract class AzureActionBase
    {
        protected AzureActionBase(ILog log)
        {
            Log = log;
        }

        ILog Log { get; }

        protected async Task<TReturn> ThrowIfNotSuccess<TResponse, TReturn>(Func<Task<AzureOperationResponse<TResponse>>> azureResponse, Func<AzureOperationResponse<TResponse>, TReturn> onSuccess, string errorMessage)
        {
            AzureOperationResponse<TResponse> operationResponse;
            try
            {
                operationResponse = await azureResponse().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Warn(e, errorMessage);
                throw new Exception(errorMessage);
            }

            if (operationResponse.Response.IsSuccessStatusCode)
            {
                return onSuccess(operationResponse);
            }

            Log.Warn($"{errorMessage}{Environment.NewLine}Response status code does not indicate success: {operationResponse.Response.StatusCode} ({operationResponse.Response.ReasonPhrase}).");
            throw new Exception(errorMessage);
        }
    }
}