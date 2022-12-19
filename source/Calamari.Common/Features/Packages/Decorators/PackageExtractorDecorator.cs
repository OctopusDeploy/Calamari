namespace Calamari.Common.Features.Packages.Decorators
{
    /// <summary>
    /// A base Decorator which allows addition to the behaviour of an IPackageExtractor 
    /// </summary>
    public class PackageExtractorDecorator : IPackageExtractor
    {
        readonly IPackageExtractor concreteExtractor;

        protected PackageExtractorDecorator(IPackageExtractor concreteExtractor)
        {
            this.concreteExtractor = concreteExtractor;
        }

        public string[] Extensions => concreteExtractor.Extensions;
        public virtual int Extract(string packageFile, string directory)
        {
            return concreteExtractor.Extract(packageFile, directory);
        }
    }
}
