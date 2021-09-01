using System;

namespace Sashimi.Server.Contracts
{
    public interface IFormatIdentifier
    {
        bool IsJson(string template);
        bool IsYaml(string template);
    }
}