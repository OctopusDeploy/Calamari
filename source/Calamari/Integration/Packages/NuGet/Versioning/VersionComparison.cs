// This was ported from https://github.com/NuGet/NuGet.Client, as the NuGet libraries are .NET 4.5 and Calamari is .NET 4.0
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Versioning
{
    /// <summary>
    /// Version comparison modes.
    /// </summary>
    public enum VersionComparison
    {
        /// <summary>
        /// Semantic version 2.0.1-rc comparison with additional compares for extra NuGetVersion fields.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Compares only the version numbers.
        /// </summary>
        Version = 1,

        /// <summary>
        /// Include Version number and Release labels in the compare.
        /// </summary>
        VersionRelease = 2,

        /// <summary>
        /// Include all metadata during the compare.
        /// </summary>
        VersionReleaseMetadata = 3
    }
}
