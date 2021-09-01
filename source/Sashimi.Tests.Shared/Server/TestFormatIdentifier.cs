using System;
using Sashimi.Server.Contracts;

namespace Sashimi.Tests.Shared.Server
{
    public class TestFormatIdentifier : IFormatIdentifier
    {
        public bool IsJson(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
                return false;

            var enclosedInCurlies = template.StartsWith("{") && template.EndsWith("}");
            var enclosedInSquares = template.StartsWith("[") && template.EndsWith("]");
            return enclosedInCurlies || enclosedInSquares;
        }

        public bool IsYaml(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
                return false;

            return !IsJson(template);
        }
    }
}