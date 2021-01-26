using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Azure;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest.Azure;

namespace Calamari.AzureAppService
{
    public static class Extensions
    {
        /// <summary>Gets the application settings of an app.</summary>
        /// <remarks>
        /// Description for Gets the application settings of an app.
        /// </remarks>
        /// <param name="operations">
        /// The operations group for this extension method.
        /// </param>
        /// <param name="targetSite">The target site containing the resource group name, site and (optional) site name</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public static async Task<StringDictionary> ListApplicationSettingsAsync(this IWebAppsOperations operations,
            TargetSite targetSite, CancellationToken cancellationToken = default)
        {
            StringDictionary body;
            if(targetSite.HasSlot)
            {
                using var operationResponse = await operations.ListApplicationSettingsSlotWithHttpMessagesAsync(targetSite.ResourceGroupName, targetSite.Site, targetSite.Slot, cancellationToken: cancellationToken).ConfigureAwait(false);
                body = operationResponse.Body;
            }
            else
            {
                using var operationResponse = await operations.ListApplicationSettingsWithHttpMessagesAsync(targetSite.ResourceGroupName, targetSite.Site, cancellationToken: cancellationToken).ConfigureAwait(false);
                body = operationResponse.Body;
            }
            return body;
        }

        public static async Task<StringDictionary> UpdateApplicationSettingsAsync(this IWebAppsOperations operations, TargetSite targetsite, StringDictionary appSettings, CancellationToken cancellationToken = default)
        {
            StringDictionary body;
            if (targetsite.HasSlot)
            {
                using var operationResponse = await operations.UpdateApplicationSettingsSlotWithHttpMessagesAsync(
                    targetsite.ResourceGroupName, targetsite.Site, appSettings, targetsite.Slot,
                    cancellationToken: cancellationToken);
                body = operationResponse.Body;
            }
            else
            {
                using var operationResponse = await operations
                    .UpdateApplicationSettingsWithHttpMessagesAsync(targetsite.ResourceGroupName, targetsite.Site, appSettings,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                body = operationResponse.Body;
            }
            return body;
        }

        public static async Task RestartAsync(this IWebAppsOperations operations, TargetSite targetSite,
            bool? softRestart = null, bool? synchronous = null, CancellationToken cancellationToken = default)
        {
            if (targetSite.HasSlot)
            {
                await operations.RestartSlotWithHttpMessagesAsync(targetSite.ResourceGroupName, targetSite.Site,
                    targetSite.Slot, softRestart, synchronous, cancellationToken: cancellationToken);
            }
            else
            {
                await operations.RestartWithHttpMessagesAsync(targetSite.ResourceGroupName, targetSite.Site,
                    softRestart, synchronous, cancellationToken: cancellationToken);
            }
        }
    }
}
