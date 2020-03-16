using System;

namespace Octopus.Sashimi.Contracts
{
    public interface IFormatIdentifier
    {
        bool IsJson(string template);
        bool IsYaml(string template);
    }
}