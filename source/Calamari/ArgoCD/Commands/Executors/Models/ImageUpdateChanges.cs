using System;
using System.Collections.Generic;

namespace Octopus.Core.Features.Kubernetes.ArgoCd.Models;

public record ImageUpdateChanges(Dictionary<string, string> UpdatedFiles, HashSet<string> UpdatedImageReferences);
