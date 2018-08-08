using System;

namespace Calamari.Shared.Commands
{
    public interface IOptionsBuilder
    {
        IOptionsBuilder Add(string prototype, string description, Action<string> action);
        IOptionsBuilder Add(string prototype, Action<string> action);
    }
}