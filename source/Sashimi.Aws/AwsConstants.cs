using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;

namespace Sashimi.Aws
{
    class AwsConstants
    {
        public static readonly string[] CloudTemplateProviderIds ={ "CloudFormation", "CF", "AWSCloudFormation" };
        public static readonly ActionHandlerCategory AwsActionHandlerCategory = new ActionHandlerCategory("Aws", "AWS", 600);
        public static CalamariFlavour CalamariAws = new CalamariFlavour("Calamari.Aws");
    }
}