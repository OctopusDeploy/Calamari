namespace Calamari.Deployment
{
    public class PathToPackage
    {
        readonly string path;

        public PathToPackage(string path)
        {
            this.path = path;
        }

        public static implicit operator string(PathToPackage pathToPackage)
            => pathToPackage.path;
    }
}