using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
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
            if (targetSite.HasSlot)
            {
                using var operationResponse = await operations
                    .ListApplicationSettingsSlotWithHttpMessagesAsync(targetSite.ResourceGroupName, targetSite.Site,
                        targetSite.Slot, cancellationToken: cancellationToken).ConfigureAwait(false);
                body = operationResponse.Body;
            }
            else
            {
                using var operationResponse = await operations
                    .ListApplicationSettingsWithHttpMessagesAsync(targetSite.ResourceGroupName, targetSite.Site,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                body = operationResponse.Body;
            }

            return body;
        }

        public static async Task<StringDictionary> UpdateApplicationSettingsAsync(this IWebAppsOperations operations,
            TargetSite targetSite, StringDictionary appSettings, CancellationToken cancellationToken = default)
        {
            if (targetSite.HasSlot)
            {
                using var operationResponse = await operations.UpdateApplicationSettingsSlotWithHttpMessagesAsync(
                    targetSite.ResourceGroupName, targetSite.Site, appSettings, targetSite.Slot,
                    cancellationToken: cancellationToken);
                return operationResponse.Body;
            }
            else
            {
                using var operationResponse = await operations
                    .UpdateApplicationSettingsWithHttpMessagesAsync(targetSite.ResourceGroupName, targetSite.Site,
                        appSettings,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                return operationResponse.Body;
            }
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

        public static async Task<SiteConfigResource> GetConfigurationAsync(this IWebAppsOperations operations,
            TargetSite targetSite, CancellationToken cancellationToken = default)
        {
            if (targetSite.HasSlot)
            {
                return (await operations.GetConfigurationSlotWithHttpMessagesAsync(targetSite.ResourceGroupName,
                    targetSite.Site,
                    targetSite.Slot, cancellationToken: cancellationToken)).Body;
            }

            return (await operations.GetConfigurationWithHttpMessagesAsync(targetSite.ResourceGroupName,
                targetSite.Site, cancellationToken: cancellationToken)).Body;
        }

        public static async Task UpdateConfigurationAsync(this IWebAppsOperations operations, TargetSite targetSite,
            SiteConfigResource config, CancellationToken cancellationToken = default)
        {
            if (targetSite.HasSlot)
            {
                await operations.UpdateConfigurationSlotWithHttpMessagesAsync(targetSite.ResourceGroupName,
                    targetSite.Site, config, targetSite.Slot, cancellationToken: cancellationToken);
            }
            else
            {
                await operations.UpdateConfigurationWithHttpMessagesAsync(targetSite.ResourceGroupName,
                    targetSite.ScmSiteAndSlot, config, cancellationToken: cancellationToken);
            }
        }

        public static async Task<ConnectionStringDictionary> ListConnectionStringsAsync(this IWebAppsOperations operations, TargetSite targetSite,
            CancellationToken cancellationToken = default)
        {
            ConnectionStringDictionary body;
            if (targetSite.HasSlot)
            {
                var response = await operations.ListConnectionStringsSlotWithHttpMessagesAsync(targetSite.ResourceGroupName,
                    targetSite.Site, targetSite.Slot, cancellationToken: cancellationToken).ConfigureAwait(false);
                body = response.Body;
            }
            else
            {
                var response = await operations.ListConnectionStringsWithHttpMessagesAsync(targetSite.ResourceGroupName,
                    targetSite.Site, cancellationToken: cancellationToken).ConfigureAwait(false);
                body = response.Body;
            }

            return body;
        }

        public static async Task UpdateConnectionStringsAsync(this IWebAppsOperations operations, TargetSite targetSite,
            ConnectionStringDictionary connectionStringDictionary, CancellationToken cancellationToken = default)
        {
            if (targetSite.HasSlot)
            {
                await operations.UpdateConnectionStringsSlotWithHttpMessagesAsync(targetSite.ResourceGroupName,
                    targetSite.Site, connectionStringDictionary, targetSite.Slot, cancellationToken: cancellationToken);
            }
            else
            {
                await operations.UpdateConnectionStringsWithHttpMessagesAsync(targetSite.ResourceGroupName,
                    targetSite.Site, connectionStringDictionary, cancellationToken: cancellationToken);
            }
        }
    }
}
