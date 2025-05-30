using System;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    class PackageIdentityBuilder
    {
        PackageId packageId;
        IVersion version;
        PackagePath path;

        public PackageIdentity Build()
        {
            return new PackageIdentity(packageId, version, path);
        }

        public PackageIdentityBuilder WithPackageId(PackageId id)
        {
            packageId = id;
            return this;
        }

        public PackageIdentityBuilder WithVersion(IVersion packageVersion)
        {
            version = packageVersion;
            return this;
        }

        public PackageIdentityBuilder WithPath(PackagePath packagePath)
        {
            path = packagePath;
            return this;
        }
    }
}