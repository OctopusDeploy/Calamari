using System;

namespace Calamari.Common.Plumbing.Deployment
{
    public class PathToPackage
    {
        readonly string path;

        public PathToPackage(string path)
        {
            this.path = path;
        }

        public static implicit operator string?(PathToPackage? pathToPackage)
        {
            return pathToPackage?.path;
        }

        public override string ToString() => path;
    }
}