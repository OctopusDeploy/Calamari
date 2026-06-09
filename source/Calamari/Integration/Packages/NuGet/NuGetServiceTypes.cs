// This was ported from https://github.com/NuGet/NuGet.Client, as the NuGet libraries are .NET 4.5 and Calamari is .NET 4.0
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Calamari.Integration.Packages.NuGet
{
    internal static class NuGetServiceTypes
    {
        public static readonly string Version300beta = "/3.0.0-beta";
        public static readonly string Version300 = "/3.0.0";
        public static readonly string Version340 = "/3.4.0";
        public static readonly string Version360 = "/3.6.0";

        public static readonly string[] RegistrationsBaseUrl = { "RegistrationsBaseUrl" + Version360, "RegistrationsBaseUrl" + Version340, "RegistrationsBaseUrl" + Version300beta };
        public static readonly string PackageBaseAddress = "PackageBaseAddress" + Version300;
    }
}