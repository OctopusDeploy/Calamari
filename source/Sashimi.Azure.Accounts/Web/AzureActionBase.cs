using System;
using System.Threading.Tasks;
using Microsoft.Rest.Azure;
using Octopus.Diagnostics;

namespace Sashimi.Azure.Accounts.Web
{
    abstract class AzureActionBase
    {
        protected AzureActionBase(ISystemLog systemLog)
        {
            SystemLog = systemLog;
        }

        ISystemLog SystemLog { get; }

        protected async Task<TReturn> ThrowIfNotSuccess<TResponse, TReturn>(Func<Task<AzureOperationResponse<TResponse>>> azureResponse, Func<AzureOperationResponse<TResponse>, TReturn> onSuccess, string errorMessage)
        {
            AzureOperationResponse<TResponse> operationResponse;
            try
            {
                operationResponse = await azureResponse().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                SystemLog.Warn(e, errorMessage);
                throw new Exception(errorMessage);
            }

            if (operationResponse.Response.IsSuccessStatusCode)
            {
                return onSuccess(operationResponse);
            }

            SystemLog.Warn($"{errorMessage}{Environment.NewLine}Response status code does not indicate success: {operationResponse.Response.StatusCode} ({operationResponse.Response.ReasonPhrase}).");
            throw new Exception(errorMessage);
        }
    }
}