namespace Octopus.Server.Orchestration.Targets.Common.BundledPackages.Transferrable
{
    public class ConsolidatedPackageFactory
    {
        readonly ConsolidatedPackageIndexLoader indexLoader = new();
        
        public ConsolidatedPackage LoadFrom(IConsolidatedPackageStreamProvider streamProvider)
        {
            using (var stream = streamProvider.OpenStream())
            {
                var index = indexLoader.Load(stream);
                return new ConsolidatedPackage(streamProvider, index);
            }
        }
    }
}
