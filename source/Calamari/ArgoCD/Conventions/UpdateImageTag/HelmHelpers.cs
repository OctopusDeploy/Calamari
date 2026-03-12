using System;
using System.Collections.Generic;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public static class HelmHelpers
{
    public static void LogHelmSourceConfigurationProblems(ILog log, IReadOnlyCollection<HelmSourceConfigurationProblem> helmSourceConfigurationProblems)
    {
        foreach (var helmSourceConfigurationProblem in helmSourceConfigurationProblems)
        {
            LogProblem(helmSourceConfigurationProblem);
        }

        void LogProblem(HelmSourceConfigurationProblem helmSourceConfigurationProblem)
        {
            switch (helmSourceConfigurationProblem)
            {
                case HelmSourceIsMissingImagePathAnnotation helmSourceIsMissingImagePathAnnotation:
                {
                    if (helmSourceIsMissingImagePathAnnotation.RefSourceIdentity == null)
                    {
                        log.WarnFormat("The Helm source '{0}' is missing an annotation for the image replace path. It will not be updated.",
                                       helmSourceIsMissingImagePathAnnotation.SourceIdentity);
                    }
                    else
                    {
                        log.WarnFormat("The Helm source '{0}' is missing an annotation for the image replace path. The source '{1}' will not be updated.",
                                       helmSourceIsMissingImagePathAnnotation.SourceIdentity,
                                       helmSourceIsMissingImagePathAnnotation.RefSourceIdentity);
                    }

                    log.WarnFormat("Annotation creation documentation can be found {0}.", log.FormatShortLink("argo-cd-helm-image-annotations", "here"));

                    return;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(helmSourceConfigurationProblem));
            }
        }
    }
}