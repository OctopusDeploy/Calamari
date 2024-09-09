using System;

namespace Calamari.FullFrameworkTools
{
    public interface IFullFrameworkCommand
    {
        string Name { get; }

        string Execute(string[] args);

        string WriteHelp();
    }
}