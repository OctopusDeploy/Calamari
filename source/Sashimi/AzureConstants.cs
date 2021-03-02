using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;

namespace Sashimi.AzureAppService
{
    static class AzureConstants
    {
        public static readonly ActionHandlerCategory AzureActionHandlerCategory = new ActionHandlerCategory("Azure", "Azure", 600);
        public static CalamariFlavour CalamariAzure = new CalamariFlavour("Calamari.AzureAppService");
    }
}