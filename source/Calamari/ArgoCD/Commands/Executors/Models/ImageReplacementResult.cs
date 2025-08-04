using System;
using System.Collections.Generic;

namespace Octopus.Core.Features.Kubernetes.ArgoCd.Models;

public record ImageReplacementResult(string UpdatedContents, HashSet<string> UpdatedImageReferences);
