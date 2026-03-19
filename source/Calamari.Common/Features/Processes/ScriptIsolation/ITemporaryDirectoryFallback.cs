using System.Collections.Generic;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

/// <summary>
/// Provides a sequence of candidate temporary directory paths to fall back to when
/// the primary lock directory does not offer sufficient lock support.
/// Separating this from the static logic in <see cref="LockDirectory"/> allows
/// hermetic unit tests that are independent of environment variables and the real
/// filesystem layout.
/// </summary>
interface ITemporaryDirectoryFallback
{
    /// <summary>
    /// Returns an ordered sequence of absolute directory paths to try as fallback
    /// lock locations for the given <paramref name="candidatePath"/>.  The sequence
    /// should be ordered from most-preferred to least-preferred.
    /// </summary>
    IEnumerable<string> GetCandidates(string candidatePath);
}
