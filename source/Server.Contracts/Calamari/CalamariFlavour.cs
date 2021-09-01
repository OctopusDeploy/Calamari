using System;

namespace Sashimi.Server.Contracts.Calamari
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