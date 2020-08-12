using System;
using Sashimi.Azure.Accounts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.CommandBuilders;

namespace Sashimi.AzureWebApp
{
    static class AzureActionHandlerExtensions
    {
       
        public static ICalamariCommandBuilder WithCheckAccountIsNotManagementCertificate(this ICalamariCommandBuilder builder, IActionHandlerContext context)
        {
            if (context.Variables.Get(SpecialVariables.AccountType) != AccountTypes.AzureServicePrincipalAccountType.ToString())
            {
                context.Log.Warn("Azure have announced they will be retiring Service Management API support on June 30th 2018. Please switch to using Service Principals for your Octopus Azure accounts https://g.octopushq.com/AzureServicePrincipalAccount");
            }

            return builder;
        }
    }
}