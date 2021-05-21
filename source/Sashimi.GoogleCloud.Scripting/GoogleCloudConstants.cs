using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;

namespace Sashimi.GCPScripting
{
    static class GoogleCloudConstants
    {
        public static readonly ActionHandlerCategory GoogleCloudActionHandlerCategory = new ActionHandlerCategory("Google", "Google Cloud", 500);
        public static readonly CalamariFlavour CalamariAzure = new CalamariFlavour("Calamari.GoogleCloudScripting");
    }
}