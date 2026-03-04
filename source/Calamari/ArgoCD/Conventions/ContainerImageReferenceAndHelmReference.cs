using System;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD.Conventions;

public record ContainerImageReferenceAndHelmReference(ContainerImageReference ContainerReference, string? HelmReference = null);