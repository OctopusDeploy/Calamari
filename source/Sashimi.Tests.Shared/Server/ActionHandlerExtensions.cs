using System;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.Tests.Shared.Server
{
    public static class ActionHandlerExtensions
    {
        public static ActionHandlerBuilder WithArrange(this IActionHandler handler, Action<TestVariableDictionary> context)
        {
            var variableDictionary = new TestVariableDictionary();
            context(variableDictionary);

            return new ActionHandlerBuilder(handler, variableDictionary);
        }
    }
}