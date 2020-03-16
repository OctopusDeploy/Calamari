namespace Sashimi.Server.Contracts.Calamari
{
    public class CalamariFlavour
    {
        public static readonly CalamariFlavour Calamari = new CalamariFlavour("Calamari");
        public static CalamariFlavour CalamariAws = new CalamariFlavour("Calamari.Cloud");
        public static readonly CalamariFlavour CalamariAzure = new CalamariFlavour("Calamari.Cloud");
        public static readonly CalamariFlavour CalamariTerraform = new CalamariFlavour("Calamari.Cloud");

        public CalamariFlavour(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }
}