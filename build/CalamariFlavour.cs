using System;

namespace Octopus.Server.Extensibility.Sashimi.Server.Contracts.Calamari
{
    public class CalamariFlavour
    {
        public CalamariFlavour(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }
}
