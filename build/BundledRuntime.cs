using System;

namespace Calamari.Build
{
    /// <summary>
    /// The .NET runtime version bundled into the published Calamari artifacts.
    /// </summary>
    /// <remarks>
    /// Calamari publishes self-contained, so every artifact carries its own copy of the .NET runtime and
    /// customer vulnerability scanners report against <em>that</em> runtime, not against the machine's.
    /// <para>
    /// Without this pin the bundled version is whatever runtime pack the build agent's SDK happens to ship -
    /// so the security posture of a release would depend on when the agent was last updated rather than on a
    /// decision anyone made. Two builds of the same commit on differently-patched agents would ship different
    /// runtimes.
    /// </para>
    /// <para>
    /// Setting <c>RuntimeFrameworkVersion</c> makes the choice explicit and reproducible: the runtime pack is
    /// restored from NuGet at this exact version regardless of the agent's SDK. Verified by publishing on an
    /// SDK whose default pack is older and confirming the output carries the pinned version instead.
    /// </para>
    /// <para>
    /// <b>Keep this current.</b> .NET ships security patches monthly; a stale value here silently ships a
    /// vulnerable runtime to every deployment target. Bump it when a new 8.0.x patch is released, and note
    /// .NET 8 reaches end of support on 10 November 2026, after which no patches exist and this must move to
    /// a supported major.
    /// </para>
    /// </remarks>
    public static class BundledRuntime
    {
        /// <summary>Latest .NET 8 patch as at 2026-08-01 (released 2026-07-14, a security update).</summary>
        public const string Version = "8.0.29";
    }
}
