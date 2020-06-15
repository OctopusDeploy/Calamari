using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.Tests.Shared.Server
{
    public class ActionHandlerBuilder
    {
        readonly IActionHandler handler;
        readonly TestVariableDictionary variableDictionary;

        internal ActionHandlerBuilder(IActionHandler handler, TestVariableDictionary variableDictionary)
        {
            this.handler = handler;
            this.variableDictionary = variableDictionary;
        }

        public void Execute()
        {
            handler.Execute(new WrapperActionHandlerContext(variableDictionary));
        }
    }
}